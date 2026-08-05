using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

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

        var detailsBefore = await GetAsync(client, $"/Intake/{receiptId:D}");
        Assert.Contains("Register Image intake", detailsBefore);
        Assert.Contains("No readable registration", detailsBefore);

        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var registerResponse = await client.PostAsync(
            $"/Intake/{receiptId:D}?handler=RegisterImageIntake",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["operationKey"] = Guid.NewGuid().ToString("N"),
                ["vehicleRegistration"] = "ab12 cde",
                ["reason"] = "Staff read the registration from the retained image."
            }));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

        var detailsAfter = await GetAsync(client, $"/Intake/{receiptId:D}");
        Assert.Contains("Image intake registered", detailsAfter);
        Assert.Contains("AB12CDE-01", detailsAfter);
        Assert.DoesNotContain("Register Image intake</h2>", detailsAfter);

        var queue = await GetAsync(client, "/Intake?decision=image_intake_registered");
        Assert.Contains("Image intake registered", queue);
        var sortingQueue = await GetAsync(client, "/Intake?decision=needs_sorting");
        Assert.DoesNotContain("vehicle.png", sortingQueue);

        var indexByReference = await GetAsync(client, "/ImageIntake?query=AB12CDE-01");
        Assert.Contains("AB12CDE-01", indexByReference);
        var indexByVrm = await GetAsync(client, "/ImageIntake?query=AB12CDE");
        Assert.Contains("AB12CDE-01", indexByVrm);

        var caseSearch = await GetAsync(client, "/Cases?query=AB12CDE-01");
        Assert.Contains("AB12CDE-01", caseSearch);
        var caseSearchImagesOnly = await GetAsync(client, "/Cases?kind=images");
        Assert.Contains("AB12CDE-01", caseSearchImagesOnly);

        await using var scope = factory.Services.CreateAsyncScope();
        var detail = await scope.ServiceProvider
            .GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None);
        var imageIntakePage = await GetAsync(client, $"/ImageIntake/{detail!.Record.Id:D}");
        Assert.Contains("AB12CDE-01", imageIntakePage);
        Assert.Contains("awaiting definitive instruction", imageIntakePage);
    }

    [Fact]
    public async Task ConfidentReadAutoRegistersAndAutoAssociatesTheUnambiguousCase()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);

        var caseEmail = IntakeTestEvidence.CreateEmail(
            "auto-case.eml",
            "QDOS instruction\r\nClaim Number: AUTO-WEB-01\r\nVehicle Registration: AB12 CDE");
        var caseUpload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            caseEmail.FileName,
            caseEmail.MediaType,
            caseEmail.Content);
        var caseOriginReceiptId = IntakeWebDriver.ReceiptId(caseUpload);

        // The instruction creates its own case now, at processing time. Seeding
        // a second one for the same receipt would make the registration match
        // ambiguous, which is a fixture artefact rather than anything the
        // product does. The case is moved to Review because that is the state
        // this test is about — an image joining an eligible case.
        var caseId = await ImageIntakeTestData.PromoteAllocatedCaseAsync(
            factory.Services,
            caseOriginReceiptId,
            nameof(CaseLifecycleState.Review));

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.Equal(IntakeDecision.ImageIntakeRegistered, receipt!.Decision);
        Assert.Equal(caseId, receipt.CurrentCaseId);

        var detail = await services
            .GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None);
        Assert.Equal("AB12CDE-01", detail!.Record.ImageIntakeReference);
        Assert.Equal(caseId, detail.AssociatedCaseId);
        // The reference is allocated by the sequence, not chosen by the fixture.
        Assert.False(string.IsNullOrWhiteSpace(detail.AssociatedCaseReference));

        var suggestions = await services
            .GetRequiredService<IVrmSuggestionStore>()
            .ListForReceiptAsync(receiptId, CancellationToken.None);
        var suggestion = Assert.Single(suggestions);
        Assert.Equal(ImageVrmSuggestionDisposition.Confirmed, suggestion.Disposition);

        var receiptPage = await GetAsync(client, $"/Intake/{receiptId:D}");
        Assert.Contains("Associated with Case", receiptPage);
        Assert.Contains("AB12CDE-01", receiptPage);
        var casePage = await GetAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("AB12CDE-01", casePage);
    }

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
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
        var caseId = (Guid)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException(
                "Processing did not allocate a case for the instruction receipt."));

        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"UPDATE CaseWorkflows SET State = {workflowState} WHERE CaseId = {caseId}");
        return caseId;
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
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Auto provider {reference}"}, {0L})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {reference}, {lineageId}, {true}, {0L})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {"not_ready"}, {"pending"}, {originReceiptId}, {true}, {true}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {workflowState}, {0L}, {Guid.NewGuid()})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", System.Globalization.CultureInfo.InvariantCulture)}, {now}, {"image-intake-test-reader"}, {"1"}, {"image-intake-fixture"}, {1}, {reference}, {1}, {true}, {now})");
        return caseId;
    }
}
