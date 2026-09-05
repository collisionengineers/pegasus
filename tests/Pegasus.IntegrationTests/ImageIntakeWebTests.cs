using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
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

        var detailsBefore = await IntakeWebDriver.GetHtmlAsync(client, $"/Received/{receiptId:D}");
        Assert.Contains("Register Image intake", detailsBefore);
        Assert.Contains("No readable registration", detailsBefore);

        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var registerResponse = await client.PostAsync(
            $"/Received/{receiptId:D}?handler=RegisterImageIntake",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["operationKey"] = Guid.NewGuid().ToString("N"),
                ["vehicleRegistration"] = "ab12 cde",
                ["reason"] = "Staff read the registration from the retained image."
            }));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

        var detailsAfter = await IntakeWebDriver.GetHtmlAsync(client, $"/Received/{receiptId:D}");
        Assert.Contains("Vehicle images registered", detailsAfter);
        Assert.Contains("AB12CDE-01", detailsAfter);
        Assert.DoesNotContain("Register Image intake</h2>", detailsAfter);

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

        // Manual uploads have no persisted mailbox classification, so automatic
        // allocation records a truthful case-type-unavailable failure. The
        // image scenario then supplies the explicit staff acceptance that
        // makes the instruction an eligible case before moving it to Review.
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

        var receiptPage = await IntakeWebDriver.GetHtmlAsync(client, $"/Received/{receiptId:D}");
        Assert.Contains("Associated with Case", receiptPage);
        Assert.Contains("AB12CDE-01", receiptPage);
        var casePage = await IntakeWebDriver.GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}?section=files");
        Assert.Contains("AB12CDE-01", casePage);
    }

    [Fact]
    public async Task StaffSetsReplacesAndClearsTheImageIntakePrincipal()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);
        var firstPrincipalId = await ImageIntakeTestData.SeedPrincipalAsync(
            factory.Services, "ALPHA", isActive: true);
        var secondPrincipalId = await ImageIntakeTestData.SeedPrincipalAsync(
            factory.Services, "BETA", isActive: true);
        var inactivePrincipalId = await ImageIntakeTestData.SeedPrincipalAsync(
            factory.Services, "INACTIVE", isActive: false);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IImageIntakeStore>();
        var detail = await store.GetByOriginReceiptAsync(
            IntakeWebDriver.ReceiptId(upload),
            CancellationToken.None);
        Assert.NotNull(detail);
        var path = $"/VehicleImages/{detail.Record.Id:D}";
        var html = await IntakeWebDriver.GetHtmlAsync(client, path);
        AssertPrincipalFact(html, "Not known");
        Assert.Contains($"value=\"{firstPrincipalId:D}\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"value=\"{secondPrincipalId:D}\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"value=\"{inactivePrincipalId:D}\"", html, StringComparison.OrdinalIgnoreCase);

        await PostPrincipalAsync(client, path, firstPrincipalId, detail.LifecycleVersion);
        detail = await store.GetAsync(detail.Record.Id, CancellationToken.None);
        html = await IntakeWebDriver.GetHtmlAsync(client, path);
        AssertPrincipalFact(html, "ALPHA");

        await PostPrincipalAsync(client, path, secondPrincipalId, detail!.LifecycleVersion);
        detail = await store.GetAsync(detail.Record.Id, CancellationToken.None);
        html = await IntakeWebDriver.GetHtmlAsync(client, path);
        AssertPrincipalFact(html, "BETA");

        await PostPrincipalAsync(client, path, null, detail!.LifecycleVersion);
        html = await IntakeWebDriver.GetHtmlAsync(client, path);
        AssertPrincipalFact(html, "Not known");
    }

    private static async Task PostPrincipalAsync(
        HttpClient client,
        string path,
        Guid? principalId,
        long expectedVersion)
    {
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var response = await client.PostAsync(
            $"{path}?handler=Principal",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["principalId"] = principalId?.ToString("D") ?? string.Empty,
                ["expectedVersion"] = expectedVersion.ToString(CultureInfo.InvariantCulture)
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static void AssertPrincipalFact(string html, string expected)
    {
        var match = Regex.Match(
            html,
            @"<dt>\s*Principal\s*</dt>\s*<dd>\s*(?<value>[^<]*)\s*</dd>",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        var value = match.Groups["value"].Value.Trim();
        Assert.Equal(expected, value);
        if (expected == "Not known")
        {
            Assert.DoesNotContain(value, new[] { string.Empty, "None", "Unknown", "Unassigned" });
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
    public static async Task<Guid> SeedPrincipalAsync(
        IServiceProvider services,
        string code,
        bool isActive)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<
                Pegasus.Infrastructure.Persistence.PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Principal {code}"}, {0L})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {code}, {lineageId}, {isActive}, {0L})");
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
            $"QDOS instruction\r\nClaimant Name: Fixture Claimant\r\nClaim Number: {claimNumber}\r\nVehicle Registration: {registration}");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);
        return await PromoteAllocatedCaseAsync(
            factory.Services,
            IntakeWebDriver.ReceiptId(upload),
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
        if (caseId is null || caseId is DBNull)
        {
            var receipt = Assert.IsType<IntakeReceipt>(
                await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(originReceiptId, CancellationToken.None));
            var failure = Assert.IsType<IntakeAllocationState>(receipt.AllocationState);
            Assert.Equal(IntakeAllocationFailureKind.CaseTypeUnavailable, failure.FailureKind);
            var accepted = await scope.ServiceProvider
                .GetRequiredService<IAcceptIntake>()
                .ExecuteAsync(
                    new(
                        receipt.Id,
                        receipt.Version,
                        ActionActor.SystemWorker("image-intake-integration"),
                        $"image-case-accept:{Guid.NewGuid():N}",
                        "Staff confirmed the manually uploaded instruction before image association.",
                        CaseType.Inspection,
                        QdosPrincipal.Code,
                        new(true, true, true, true)),
                    CancellationToken.None);
            caseId = accepted.Identity.CaseId;
        }
        var caseIdValue = (Guid)caseId;

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
