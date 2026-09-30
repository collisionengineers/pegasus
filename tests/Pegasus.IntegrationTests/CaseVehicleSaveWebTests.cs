using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Vehicle;
using Pegasus.Web.Authentication;
using Pegasus.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Reproduces the pre-v1 incident through the composed Web host. The accepted
/// registration stays a Fact while the Case workspace records a corrected Make.
/// Details, the Cases queue and Search must all remain readable afterwards.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseVehicleSaveWebTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SavingThenClearingMakeKeepsAcceptedRegistrationAndCaseSurfacesReadable()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        await SeedAcceptedWorkspaceValuesAsync(factory.Services, receipt.Id);
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    actor,
                    "accept-case-vehicle-save-web",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true),
                    AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                CancellationToken.None);
        var caseId = accepted.Identity.CaseId;

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        var available = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        using (var claim = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=ClaimLease",
                   Form(
                       AntiforgeryValue(available),
                       ("id", caseId.ToString("D")),
                       ("expectedVersion", InputValue(available, "expectedVersion")),
                       ("operationKey", InputValue(available, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
        }

        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Equal("AB12CDE", InputValue(editing, "vehicleRegistration"));
        Assert.Equal(string.Empty, InputValue(editing, "vehicleMake"));
        var before = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(before);
        Assert.Equal("Jane Example", before!.Claimant.Name.Fact?.Value);
        Assert.Equal("QDOS-123", before.Claim.Number.Fact?.Value);
        Assert.Equal(CaseDataSourceKind.IntakeEvidence, before.Claimant.Name.Fact?.Source.Kind);
        Assert.Equal(CaseDataSourceKind.IntakeEvidence, before.Claim.Number.Fact?.Source.Kind);
        using (var save = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(editing),
                       CurrentCaseSaveValues(editing, caseId))))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        var clearing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("data-case-editing=\"true\"", clearing, StringComparison.Ordinal);
        Assert.Equal("Ford", InputValue(clearing, "vehicleMake"));
        using (var clear = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(clearing),
                       CurrentCaseSaveValues(
                           clearing,
                           caseId,
                           vehicleMake: string.Empty,
                           reason: "Cleared an incorrectly recorded vehicle make."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, clear.StatusCode);
        }

        var unchanged = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("data-case-editing=\"true\"", unchanged, StringComparison.Ordinal);
        Assert.Equal(string.Empty, InputValue(unchanged, "vehicleMake"));
        using (var save = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(unchanged),
                       CurrentCaseSaveValues(
                           unchanged,
                           caseId,
                           vehicleMake: string.Empty,
                           reason: "Recorded vehicle facts remain unchanged."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        var details = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("AB12CDE", details, StringComparison.Ordinal);
        var vehicle = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=vehicle");
        Assert.Contains("AB12CDE", vehicle, StringComparison.Ordinal);

        var cases = await GetHtmlAsync(client, $"/Cases?tab=review&selected={caseId:D}");
        Assert.Contains(caseId.ToString("D"), cases, StringComparison.Ordinal);

        var search = await GetHtmlAsync(client, "/Search?registration=AB12CDE");
        Assert.Contains(caseId.ToString("D"), search, StringComparison.Ordinal);

        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        var evidence = await scope.ServiceProvider.GetRequiredService<IVehicleEvidenceQueries>()
            .GetAsync(caseId, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Equal(before.Claimant, data!.Claimant);
        Assert.Equal(before.Claim, data.Claim);
        Assert.Equal(before.Accident, data.Accident);
        Assert.Equal(before.Contact, data.Contact);
        Assert.Equal(before.Instruction, data.Instruction);
        Assert.Equal(before.Inspection, data.Inspection);
        Assert.Equal("AB12CDE", data!.Vehicle.Registration.Fact?.Value);
        Assert.Null(data.Vehicle.Registration.Confirmed);
        Assert.Equal(before.Vehicle.Model, data.Vehicle.Model);
        Assert.Equal(before.Vehicle.Mileage, data.Vehicle.Mileage);
        Assert.Equal(before.Vehicle.MileageUnit, data.Vehicle.MileageUnit);
        Assert.Null(data.Vehicle.Make.Confirmed);
        Assert.NotNull(evidence);
        Assert.Null(evidence!.Confirmed);
    }

    /// <summary>
    /// What the Case page costs in SQL commands in an edit session, and what one
    /// accepted single-field save costs, against the real stores (Roadmap Lane
    /// D, parts D2 and D5). Every save-as-you-go commit is that save followed by
    /// the page, so both are the price of one field. The budgets are upper
    /// bounds, so a lower count passes and a page that grows fails.
    /// </summary>
    [Fact]
    public async Task TheCasePageInAnEditSessionAndOneAcceptedSaveStayWithinTheirStatementBudgets()
    {
        var counter = new CommandCountingInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-statement-count", withMileage: false);
        using var client = CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        // The first render of each shape pays one-off work; the counts are later ones.
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}");

        counter.Reset();
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var pageCommands = counter.Count;
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);

        counter.Reset();
        using (var save = await client.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(editing),
                       CurrentCaseSaveValues(
                           editing,
                           caseId,
                           vehicleMake: "Vauxhall",
                           reason: "Statement count fixture."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }
        var saveCommands = counter.Count;

        // Before Lane D (2ee268507), by reading the code: about 70 for the page and
        // about 38 for the save. After: about 69 and about 38 (the folds the reading found
        // for the save each change the command, the lease or the conflict check, or the
        // ports many test fakes implement, and are left). Each budget adds headroom for
        // the data shape; the readings are estimates. To be confirmed by CI.
        Assert.True(
            pageCommands <= CasePageBudget,
            $"The Case page in an edit session sent {pageCommands} SQL commands; the budget is {CasePageBudget}.");
        Assert.True(
            saveCommands <= CaseSaveBudget,
            $"One accepted save sent {saveCommands} SQL commands; the budget is {CaseSaveBudget}.");
    }

    private const int CasePageBudget = 72;

    private const int CaseSaveBudget = 42;

    /// <summary>
    /// Roadmap Lane H (FRD-16): a commit the page script posts is answered with
    /// the parts it swaps, not with a redirect to the whole page. Three commits
    /// walk the real stores the way the script does: each carries the version,
    /// lease and operation key the answer before it returned, so an answer that
    /// carried the wrong authority fails the next save. The first two are
    /// answered in place; the third has no script header and keeps its redirect,
    /// which prices what the script used to follow. Numbers are printed in the
    /// failure messages, and the budgets are upper bounds.
    /// </summary>
    [Fact]
    public async Task ACommitAnsweredInPlaceCarriesTheNextAuthorityAndCostsLessThanTheRedirectAndPage()
    {
        var counter = new CommandCountingInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-commit-answer", withMileage: false);
        using var client = CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        // The first render of each shape pays one-off work; the counts are later ones.
        _ = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var startVersion = long.Parse(InputValue(editing, "expectedVersion"), CultureInfo.InvariantCulture);

        // The script keeps every section as it is and takes only the authority from an answer.
        counter.Reset();
        var first = await CommitAsync(
            client, editing, caseId, CurrentCaseSaveValues(editing, caseId, vehicleMake: "Vauxhall"), script: true);
        var firstCommands = counter.Count;
        Assert.Equal(HttpStatusCode.OK, first.Status);
        var firstCommit = EditorCommit(first.Body);
        Assert.Equal(startVersion, firstCommit.ExpectedVersion);
        Assert.Equal(startVersion + 1, firstCommit.Version);
        Assert.Contains("data-case-editing=\"true\"", first.Body, StringComparison.Ordinal);
        Assert.Contains($"data-case-version=\"{startVersion + 1}\"", first.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-main\"", first.Body, StringComparison.Ordinal);

        counter.Reset();
        var second = await CommitAsync(
            client,
            editing,
            caseId,
            WithAuthority(CurrentCaseSaveValues(editing, caseId, vehicleMake: "Renault"), first.Body),
            script: true);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        var secondCommit = EditorCommit(second.Body);
        Assert.Equal(startVersion + 1, secondCommit.ExpectedVersion);
        Assert.Equal(startVersion + 2, secondCommit.Version);

        counter.Reset();
        var third = await CommitAsync(
            client,
            editing,
            caseId,
            WithAuthority(CurrentCaseSaveValues(editing, caseId, vehicleMake: "Skoda"), second.Body),
            script: false);
        var plainPostCommands = counter.Count;
        Assert.Equal(HttpStatusCode.Redirect, third.Status);
        counter.Reset();
        var page = await GetHtmlAsync(client, third.Location!);
        var pageCommands = counter.Count;
        Assert.Contains("Case saved.", page, StringComparison.Ordinal);
        Assert.Equal((startVersion + 3).ToString(CultureInfo.InvariantCulture), SaveFormValue(page, "expectedVersion"));

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.Equal("Skoda", data!.Vehicle.Make.Confirmed?.Value);

        var answerBytes = Encoding.UTF8.GetByteCount(first.Body);
        var pageBytes = Encoding.UTF8.GetByteCount(page);
        var redirectedCommands = plainPostCommands + pageCommands;
        output.WriteLine(
            $"Lane H: an answered commit sent {firstCommands} SQL commands and {answerBytes} bytes; "
            + $"the same commit with its redirect sent {redirectedCommands} ({plainPostCommands} + {pageCommands}) "
            + $"and {pageBytes} bytes.");
        // Before Lane H, by reading the code: the save (about 38) plus the redirected page
        // (about 69), and the whole page in the response (about 42 KB). After: the save plus
        // the reads the swapped parts need. To be confirmed by CI.
        Assert.True(
            firstCommands < redirectedCommands,
            $"An answered commit sent {firstCommands} SQL commands; the save and its redirected page send {redirectedCommands}.");
        Assert.True(
            firstCommands <= CommitAnswerBudget,
            $"An answered commit sent {firstCommands} SQL commands; the budget is {CommitAnswerBudget}.");
        Assert.True(
            answerBytes < pageBytes * 6 / 10,
            $"The answer is {answerBytes} bytes; the page it replaces is {pageBytes}.");
    }

    /// <summary>The save (about 38) and the answer's own reads (about 37).</summary>
    private const int CommitAnswerBudget = 86;

    private static async Task<(HttpStatusCode Status, string Body, string? Location)> CommitAsync(
        HttpClient client,
        string antiforgeryHtml,
        Guid caseId,
        (string Name, string Value)[] fields,
        bool script)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Cases/{caseId:D}?handler=Save")
        {
            Content = Form(AntiforgeryValue(antiforgeryHtml), fields)
        };
        if (script)
        {
            request.Headers.Add("X-Requested-With", "fetch");
        }
        using var response = await client.SendAsync(request);
        return (
            response.StatusCode,
            await response.Content.ReadAsStringAsync(),
            response.Headers.Location?.OriginalString);
    }

    /// <summary>The Save form's own inputs, taken from an answer as the script's carry-forward takes them.</summary>
    private static (string Name, string Value)[] WithAuthority(
        (string Name, string Value)[] fields,
        string answer) =>
    [
        .. fields.Select(field => field.Name is "expectedVersion" or "operationKey" or "editLeaseToken"
            ? (field.Name, SaveFormValue(answer, field.Name))
            : field)
    ];

    private static string SaveFormValue(string html, string name)
    {
        var start = html.IndexOf("id=\"case-edit-form\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The answer must draw the Save form.");
        var end = html.IndexOf("</form>", start, StringComparison.Ordinal);
        return InputValue(html[start..end], name);
    }

    private static (string OperationKey, long ExpectedVersion, long Version) EditorCommit(string html)
    {
        var attribute = Regex.Match(html, "data-editor-commit=\"(?<value>[^\"]+)\"");
        Assert.True(attribute.Success, "The answer must confirm the commit.");
        using var json = JsonDocument.Parse(WebUtility.HtmlDecode(attribute.Groups["value"].Value));
        var commit = json.RootElement;
        return (
            commit.GetProperty("operationKey").GetString()!,
            commit.GetProperty("expectedVersion").GetInt64(),
            commit.GetProperty("version").GetInt64());
    }

    /// <summary>
    /// The record form defaults the unit to miles when the Case carries neither
    /// part of an odometer reading.
    /// </summary>
    [Fact]
    public async Task TypingAMileageSavesItInMiles()

    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-vehicle-mileage-typed", withMileage: false);
        using var client = CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));

        await SaveMileageAsync(client, caseId, "51234", "Recorded the mileage read at inspection.");

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Equal(51234L, data!.Vehicle.Mileage.Confirmed?.Value);
        Assert.Equal("miles", data.Vehicle.MileageUnit.Confirmed?.Value);
    }

    [Fact]
    public async Task ChangingMileageUnitToKilometresDoesNotConvertTheMileageValue()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-vehicle-mileage-kilometres", withMileage: false);
        using var client = CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));

        await SaveMileageAsync(client, caseId, "42000", "Recorded the mileage read at inspection.");
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Equal("miles", SelectValue(editing, "vehicleMileageUnit"));
        await SaveMileageAsync(
            client,
            caseId,
            "42000",
            "Changed the odometer unit to kilometres.",
            "kilometres");

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Equal(42000L, data!.Vehicle.Mileage.Confirmed?.Value);
        Assert.Equal("kilometres", data.Vehicle.MileageUnit.Confirmed?.Value);
    }

    /// <summary>
    /// Emptying the box clears the unit with it. A unit standing behind a
    /// mileage that is gone is half an odometer reading, and the case record
    /// refuses one.
    /// </summary>
    [Fact]
    public async Task ClearingAMileageRemovesItsUnitToo()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-vehicle-mileage-cleared", withMileage: false);
        using var client = CreateClient(factory);
        await ClaimLeaseAsync(client, caseId, await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        await SaveMileageAsync(client, caseId, "51234", "Recorded the mileage read at inspection.");

        await SaveMileageAsync(
            client, caseId, string.Empty, "Removed a mileage recorded against the wrong vehicle.");

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Null(data!.Vehicle.Mileage.Confirmed);
        Assert.Null(data.Vehicle.Mileage.Fact);
        Assert.Null(data.Vehicle.MileageUnit.Confirmed);
        Assert.Null(data.Vehicle.MileageUnit.Fact);
    }

    private static async Task<Guid> AcceptCaseAsync(
        IntakeWebApplicationFactory factory,
        string operationKey,
        bool withMileage)
    {
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        await SeedAcceptedWorkspaceValuesAsync(factory.Services, receipt.Id, withMileage);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    ActionActor.Staff(
                        DevelopmentOfflineIdentity.AdministratorId,
                        [StaffRole.Administrator]),
                    operationKey,
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true),
                    AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                CancellationToken.None);
        return accepted.Identity.CaseId;
    }

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

    private static async Task SaveMileageAsync(
        HttpClient client,
        Guid caseId,
        string mileage,
        string reason,
        string? mileageUnit = null)
    {
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);
        using var save = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=Save",
            Form(
                AntiforgeryValue(editing),
                CurrentCaseSaveValues(
                    editing,
                    caseId,
                    vehicleMake: InputValue(editing, "vehicleMake"),
                    reason: reason,
                    vehicleMileage: mileage,
                    vehicleMileageUnit: mileageUnit)));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
    }

    [Fact]
    public async Task ASecondStaffClientCannotClaimOrSaveOverAnActiveVehicleEditor()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.PostConfigure<PolicySchemeOptions>(
                    "Pegasus",
                    options => options.ForwardDefaultSelector = static _ =>
                        IdentityConstants.ApplicationScheme)));
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        await SeedAcceptedWorkspaceValuesAsync(factory.Services, receipt.Id);
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    0,
                    actor,
                    "accept-case-vehicle-two-editors",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true),
                    AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                CancellationToken.None);
        var caseId = accepted.Identity.CaseId;

        await CreateEngineerAsync(factory.Services, "vehicle-editor-one", "Password-1");
        await CreateEngineerAsync(factory.Services, "vehicle-editor-two", "Password-1");
        using var firstClient = await SignInAsync(factory, "vehicle-editor-one", "Password-1");
        using var secondClient = await SignInAsync(factory, "vehicle-editor-two", "Password-1");

        var firstAvailable = await GetHtmlAsync(firstClient, $"/Cases/{caseId:D}");
        var secondAvailable = await GetHtmlAsync(secondClient, $"/Cases/{caseId:D}");
        Assert.Contains("<strong>vehicle-editor-one</strong>", firstAvailable, StringComparison.Ordinal);
        Assert.Contains("<strong>vehicle-editor-two</strong>", secondAvailable, StringComparison.Ordinal);
        await ClaimLeaseAsync(firstClient, caseId, firstAvailable);
        var firstEditing = await GetHtmlAsync(firstClient, $"/Cases/{caseId:D}");

        using (var claim = await secondClient.PostAsync(
                   $"/Cases/{caseId:D}?handler=ClaimLease",
                   Form(
                       AntiforgeryValue(secondAvailable),
                       ("id", caseId.ToString("D")),
                       ("expectedVersion", InputValue(secondAvailable, "expectedVersion")),
                       ("operationKey", InputValue(secondAvailable, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
        }

        using (var overwrite = await secondClient.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(secondAvailable),
                       CurrentCaseSaveValues(
                           firstEditing,
                           caseId,
                           vehicleMake: "Renault",
                           reason: "Attempted competing vehicle correction."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, overwrite.StatusCode);
        }

        var beforeFirstSave = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(beforeFirstSave);
        Assert.Null(beforeFirstSave!.Vehicle.Make.Confirmed);

        using (var firstSave = await firstClient.PostAsync(
                   $"/Cases/{caseId:D}?handler=Save",
                   Form(
                       AntiforgeryValue(firstEditing),
                       CurrentCaseSaveValues(
                           firstEditing,
                           caseId,
                           vehicleMake: "Ford"))))
        {
            Assert.Equal(HttpStatusCode.Redirect, firstSave.StatusCode);
        }

        var saved = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("Ford", saved!.Vehicle.Make.Confirmed?.Value);
        Assert.Equal("AB12CDE", saved.Vehicle.Registration.Fact?.Value);
    }

    private static async Task SeedAcceptedWorkspaceValuesAsync(
        IServiceProvider services,
        Guid receiptId,
        bool withMileage = true)
    {
        var fields = new[]
        {
            Field("Claimant name", "Jane Example"),
            Field("Claim number", "QDOS-123"),
            Field("Vehicle registration", "AB12CDE"),
            Field("Vehicle model", "Focus"),
            Field("Vehicle mileage", "42000"),
            Field("Vehicle mileage unit", "miles"),
            Field("Accident circumstances", "Rear-end impact at a roundabout."),
            Field("Incident date", "2031-04-01"),
            Field("Inspection date", "2031-05-20"),
            Field("Inspection address", "1 Test Street, London"),
            Field("Claimant contact number", "07700 900123"),
            Field("Claimant address", "12 Example Street, Leeds, LS1 1AA"),
            Field("Contact name", "Case handler"),
            Field("Contact email", "handler@example.test"),
            Field("Contact phone", "020 7946 0123"),
            Field("VAT status", "VAT registered")
        };
        if (!withMileage)
        {
            fields = [.. fields.Where(field =>
                !field.Name.StartsWith("Vehicle mileage", StringComparison.Ordinal))];
        }

        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var draft = await context.InstructionDrafts.SingleAsync(item => item.IntakeReceiptId == receiptId);
        draft.ClaimantName = "Jane Example";
        draft.ClaimNumber = "QDOS-123";
        draft.VehicleModel = "Focus";
        draft.VehicleMileage = withMileage ? 42000 : null;
        draft.VehicleMileageUnit = withMileage ? "miles" : null;
        draft.AccidentCircumstances = "Rear-end impact at a roundabout.";
        draft.DateOfIncident = new DateOnly(2031, 4, 1);
        draft.InspectionDate = new DateOnly(2031, 5, 20);
        draft.InspectionAddress = "1 Test Street, London";
        draft.ClaimantContactNumber = "07700 900123";
        draft.ClaimantAddress = "12 Example Street, Leeds, LS1 1AA";
        draft.FileHandlerName = "Case handler";
        draft.FileHandlerEmailAddress = "handler@example.test";
        draft.FileHandlerPhoneNumber = "020 7946 0123";
        draft.VatStatus = "VAT registered";
        var receipt = await context.IntakeReceipts.SingleAsync(item => item.Id == receiptId);
        receipt.FieldsJson = EfIntakeReceiptStore.SerializeFields(fields);
        await context.SaveChangesAsync();
    }

    private static InstructionReviewField Field(string name, string value) => new(
        name,
        value,
        [new(value, IntakeEvidenceSource.DocumentContent, "retained vehicle-save test instruction")],
        IsDefaulted: false,
        HasConflict: false);

    private static (string Name, string Value)[] CurrentCaseSaveValues(
        string html,
        Guid caseId,
        string vehicleMake = "Ford",
        string reason = "Corrected vehicle make from retained instruction.",
        string? vehicleMileage = null,
        string? vehicleMileageUnit = null) =>
    [
        ("id", caseId.ToString("D")),
        ("expectedVersion", InputValue(html, "expectedVersion")),
        ("operationKey", InputValue(html, "operationKey")),
        ("editLeaseToken", InputValue(html, "editLeaseToken")),
        ("reason", reason),
        ("claimantName", InputValue(html, "claimantName")),
        ("claimantContactNumber", InputValue(html, "claimantContactNumber")),
        ("claimantAddress", InputValue(html, "claimantAddress")),
        ("claimNumber", InputValue(html, "claimNumber")),
        ("vehicleRegistration", InputValue(html, "vehicleRegistration")),
        ("vehicleMake", vehicleMake),
        ("vehicleModel", InputValue(html, "vehicleModel")),
        ("vehicleMileage", vehicleMileage ?? InputValue(html, "vehicleMileage")),
        ("vehicleMileageUnit", vehicleMileageUnit ?? SelectValue(html, "vehicleMileageUnit")),
        ("accidentCircumstances", TextareaValue(html, "accidentCircumstances")),
        ("incidentDate", InputValue(html, "incidentDate")),
        ("dueBy", InputValue(html, "dueBy")),
        ("claimSourceContactName", InputValue(html, "claimSourceContactName")),
        ("claimSourceContactTelephone", InputValue(html, "claimSourceContactTelephone")),
        ("claimSourceContactEmail", InputValue(html, "claimSourceContactEmail")),
        ("contactName", InputValue(html, "contactName")),
        ("contactEmailAddress", InputValue(html, "contactEmailAddress")),
        ("contactPhoneNumber", InputValue(html, "contactPhoneNumber")),
        ("vatStatus", InputValue(html, "vatStatus")),
        ("inspectionDate", InputValue(html, "inspectionDate")),
        ("inspectionDeadline", InputValue(html, "inspectionDeadline")),
        ("inspectionAddress", InputValue(html, "inspectionAddress")),
        ("inspectionMode", InputValue(html, "inspectionMode")),
        ("storageLocation", InputValue(html, "storageLocation"))
    ];

    private static async Task ClaimLeaseAsync(HttpClient client, Guid caseId, string html)
    {
        using var claim = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("expectedVersion", InputValue(html, "expectedVersion")),
                ("operationKey", InputValue(html, "operationKey"))));
        Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
    }

    private static async Task<HttpClient> SignInAsync(
        WebApplicationFactory<Program> factory,
        string userName,
        string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        var signIn = await GetHtmlAsync(client, "/Account/SignIn");
        using var response = await client.PostAsync(
            "/Account/SignIn",
            Form(
                AntiforgeryValue(signIn),
                ("UserName", userName),
                ("Password", password)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-Pegasus=", StringComparison.Ordinal));
        return client;
    }

    private static async Task CreateEngineerAsync(
        IServiceProvider services,
        string userName,
        string password)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsEnabled = true,
            MustChangePassword = false
        };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, StaffRole.Engineer.ToString())).Succeeded);
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Name, string Value)[] values) =>
        new(values
            .Select(value => KeyValuePair.Create(value.Name, value.Value))
            .Append(KeyValuePair.Create("__RequestVerificationToken", antiforgeryToken)));

    private static string InputValue(string html, string name)
    {
        var input = Regex.Match(
            html,
            $"<input[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(input.Success, $"The Case form must render '{name}'.");
        var value = Regex.Match(
            input.Value,
            "value=\\\"(?<value>[^\\\"]*)\\\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return value.Success
            ? WebUtility.HtmlDecode(value.Groups["value"].Value)
            : string.Empty;
    }

    private static string SelectValue(string html, string name)
    {
        var select = Regex.Match(
            html,
            $"<select[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>.*?</select>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(select.Success, $"The Case form must render '{name}' as a select.");
        var selectedOption = Regex.Match(
            select.Value,
            "<option[^>]*\\sselected(?:=\\\"[^\\\"]*\\\")?[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(selectedOption.Success, $"The Case form select '{name}' must have a selected option.");
        var value = Regex.Match(
            selectedOption.Value,
            "value=\\\"(?<value>[^\\\"]*)\\\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(value.Success, $"The selected option for '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string TextareaValue(string html, string name)
    {
        var textarea = Regex.Match(
            html,
            $"<textarea[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>(?<value>.*?)</textarea>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(textarea.Success, $"The Case form must render '{name}'.");
        return WebUtility.HtmlDecode(textarea.Groups["value"].Value);
    }

    private static string AntiforgeryValue(string html)
    {
        var value = InputValue(html, "__RequestVerificationToken");
        Assert.False(string.IsNullOrEmpty(value), "The Case antiforgery token must have a value.");
        return value;
    }
}
