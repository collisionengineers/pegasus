using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

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
        Assert.Contains($"value=\"{alpha:D}\"", initial, StringComparison.Ordinal);
        Assert.Contains($"value=\"{beta:D}\"", initial, StringComparison.Ordinal);
        Assert.DoesNotContain($"value=\"{retired:D}\"", initial, StringComparison.Ordinal);
        // The empty option is a real selectable state, not a disabled prompt.
        Assert.Contains("<option value=\"\"", initial, StringComparison.Ordinal);

        await PostPrincipalAsync(factory, client, imageIntakeId, alpha);
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

        await using var scope = factory.Services.CreateAsyncScope();
        var detail = await scope.ServiceProvider
            .GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None);
        return Assert.IsType<ImageIntakeDetail>(detail).Record.Id;
    }

    private static async Task PostPrincipalAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        Guid imageIntakeId,
        Guid? principalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var detail = await scope.ServiceProvider
            .GetRequiredService<IImageIntakeQueries>()
            .GetAsync(imageIntakeId, CancellationToken.None);
        var expectedVersion = Assert.IsType<ImageIntakeDetail>(detail).LifecycleVersion;
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var response = await client.PostAsync(
            $"/VehicleImages/{imageIntakeId:D}?handler=Principal",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["principalId"] = principalId is { } id ? id.ToString("D") : string.Empty,
                ["expectedVersion"] = expectedVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    /// <summary>
    /// The Principal fact is always drawn. The absent state is exactly
    /// `Not known` — never an empty value and never one of the alternates the
    /// rest of the product does not use.
    /// </summary>
    private static void AssertPrincipalFact(string html, string expected)
    {
        var match = Regex.Match(
            html,
            "<dt>Principal</dt>\\s*<dd>(?<value>[^<]*)</dd>");
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
}

internal static class MultiFormatFixture
{
    public const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
}

internal static class ImageIntakeTestData
{
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
            INSERT INTO [Organizations] ([Id], [Name], [Version])
            VALUES ({organizationId}, {$"Fixture organization {code}"}, 0)
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
