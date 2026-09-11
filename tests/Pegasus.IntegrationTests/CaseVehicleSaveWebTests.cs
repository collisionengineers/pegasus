using System.Net;
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

namespace Pegasus.IntegrationTests;

/// <summary>
/// Reproduces the pre-v1 incident through the composed Web host. The accepted
/// registration stays a Fact while the Case workspace records a corrected Make.
/// Details, the Cases queue and Search must all remain readable afterwards.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseVehicleSaveWebTests
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
            .GetAsync(caseId, CancellationToken.None);
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

        var clearedAvailable = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        await ClaimLeaseAsync(client, caseId, clearedAvailable);
        var clearing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
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

        var unchangedAvailable = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        await ClaimLeaseAsync(client, caseId, unchangedAvailable);
        var unchanged = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
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
            .GetAsync(caseId, CancellationToken.None);
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
    /// The record has one mileage box and no unit control beside it, so the
    /// unit is settled by the box: a figure typed onto a case carrying neither
    /// is read in miles, which is what every odometer this business inspects
    /// reads in.
    /// </summary>
    [Fact]
    public async Task TypingAMileageSavesItInMiles()
    {
        using var factory = new IntakeWebApplicationFactory(
            useIntegrationTestAuthentication: true);
        var caseId = await AcceptCaseAsync(
            factory, "accept-case-vehicle-mileage-typed", withMileage: false);
        using var client = CreateClient(factory);

        await SaveMileageAsync(client, caseId, "51234", "Recorded the mileage read at inspection.");

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Equal(51234L, data!.Vehicle.Mileage.Confirmed?.Value);
        Assert.Equal("miles", data.Vehicle.MileageUnit.Confirmed?.Value);
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
        await SaveMileageAsync(client, caseId, "51234", "Recorded the mileage read at inspection.");

        await SaveMileageAsync(
            client, caseId, string.Empty, "Removed a mileage recorded against the wrong vehicle.");

        await using var scope = factory.Services.CreateAsyncScope();
        var data = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CancellationToken.None);
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
        string reason)
    {
        var available = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        await ClaimLeaseAsync(client, caseId, available);
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        using var save = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=Save",
            Form(
                AntiforgeryValue(editing),
                CurrentCaseSaveValues(
                    editing,
                    caseId,
                    vehicleMake: InputValue(editing, "vehicleMake"),
                    reason: reason,
                    vehicleMileage: mileage)));
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
            .GetAsync(caseId, CancellationToken.None);
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
            .GetAsync(caseId, CancellationToken.None);
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
            Field("Instruction date", "2031-04-02"),
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
        draft.InstructionDate = new DateOnly(2031, 4, 2);
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
        string? vehicleMileage = null) =>
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
        ("vehicleMileageUnit", InputValue(html, "vehicleMileageUnit")),
        ("accidentCircumstances", TextareaValue(html, "accidentCircumstances")),
        ("incidentDate", InputValue(html, "incidentDate")),
        ("contactName", InputValue(html, "contactName")),
        ("contactEmailAddress", InputValue(html, "contactEmailAddress")),
        ("contactPhoneNumber", InputValue(html, "contactPhoneNumber")),
        ("instructionDate", InputValue(html, "instructionDate")),
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
