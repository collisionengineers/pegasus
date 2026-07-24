using System.Net;
using CollisionSpike.Core.Intake.Qdos;
using CollisionSpike.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CollisionSpike.IntegrationTests;

public sealed class QdosWebNegativeTests
{
    private const int TenMiB = 10 * 1024 * 1024;

    [Fact]
    public async Task MissingAntiforgeryTokenIsRejectedBeforePersistence()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);

        var result = await QdosWebDriver.PostUploadAsync(
            client,
            null,
            "payload.pdf",
            "application/octet-stream",
            [0x01],
            "cccccccccccccccccccccccccccccccc");

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task TamperedAntiforgeryTokenIsRejectedBeforePersistence()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var form = await QdosWebDriver.GetUploadFormTokensAsync(client);

        var result = await QdosWebDriver.PostUploadAsync(
            client, "tampered", "payload.pdf", "application/octet-stream", [0x01], form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task MissingUploadReturnsValidationAndDoesNotPersist()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var form = await QdosWebDriver.GetUploadFormTokensAsync(client);

        var result = await QdosWebDriver.PostUploadAsync(
            client, form.AntiforgeryToken, null, "application/octet-stream", null, form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains(
            "Choose an email, document, PDF or image to upload.",
            result.ResponseBody,
            StringComparison.Ordinal);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task WrongExtensionReturnsValidationAndDoesNotPersist()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var form = await QdosWebDriver.GetUploadFormTokensAsync(client);

        var result = await QdosWebDriver.PostUploadAsync(
            client, form.AntiforgeryToken, "payload.bin", "application/octet-stream", [0x00, 0x01], form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains(
            "Choose an .eml, .pdf, .docx, .doc, .msg, .jpg, .jpeg or .png file.",
            result.ResponseBody,
            StringComparison.Ordinal);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task EmptyUploadReturnsValidationAndDoesNotPersist()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var form = await QdosWebDriver.GetUploadFormTokensAsync(client);

        var result = await QdosWebDriver.PostUploadAsync(
            client, form.AntiforgeryToken, "payload.pdf", "application/octet-stream", [], form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("The selected file is empty.", result.ResponseBody, StringComparison.Ordinal);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task ExactTenMiBUploadPassesTransportValidation()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var form = await QdosWebDriver.GetUploadFormTokensAsync(client);

        var result = await QdosWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "boundary.pdf",
            "application/octet-stream",
            new byte[TenMiB],
            form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var receipt = Assert.Single(await ListAllAsync(factory));
        Assert.Equal(TenMiB, (await GetAsync(factory, receipt.Id)).SourceLength);
    }

    [Fact]
    public async Task TenMiBPlusOneReturnsValidationAndDoesNotPersist()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var form = await QdosWebDriver.GetUploadFormTokensAsync(client);

        var result = await QdosWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "boundary.pdf",
            "application/octet-stream",
            new byte[TenMiB + 1],
            form.ExternalReceiptToken);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("The selected file must be 10 MB or smaller.", result.ResponseBody, StringComparison.Ordinal);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task InvalidExternalReceiptTokenReturnsValidationAndDoesNotPersist()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        var form = await QdosWebDriver.GetUploadFormTokensAsync(client);

        var result = await QdosWebDriver.PostUploadAsync(
            client,
            form.AntiforgeryToken,
            "payload.pdf",
            "application/octet-stream",
            [0x00, 0x01],
            externalReceiptToken: "not-a-valid-n-format-guid");

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains(
            "upload receipt is invalid",
            result.ResponseBody,
            StringComparison.Ordinal);
        await AssertNoBusinessPersistenceAsync(factory);
    }

    [Fact]
    public async Task MissingReviewReceiptReturnsNotFound()
    {
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"/Intake/Review/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PersistedDraftValuesAreHtmlEncodedByReviewPage()
    {
        const string hostile = "<script>alert(1)</script>";
        using var factory = new QdosWebApplicationFactory();
        using var client = QdosWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IQdosIntakeStore>();
        var time = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        var record = await store.StoreAsync(new(
            hostile + ".bin",
            "application/octet-stream",
            1,
            new string('A', 64),
            new(IntakeSourceChannel.ManualUpload, "33333333333333333333333333333333"),
            time,
            time,
            "Direct persisted draft",
            QdosIntakeDecision.TechnicalFailure,
            hostile,
            [new(QdosEvidenceSource.SystemDefault, QdosEvidenceStrength.Weak,
                QdosEvidenceFinding.Information, hostile, hostile)],
            [new(hostile, hostile, [new(hostile, QdosEvidenceSource.SystemDefault, hostile)], false, false)],
            null,
            [hostile],
            hostile,
            hostile), CancellationToken.None);

        using var response = await client.GetAsync($"/Intake/Review/{record.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(hostile, html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<QdosIntakeSummary>> ListAllAsync(QdosWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>()
            .ListAsync(null, CancellationToken.None);
    }

    private static async Task<QdosIntakeRecord> GetAsync(QdosWebApplicationFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return Assert.IsType<QdosIntakeRecord>(await scope.ServiceProvider
            .GetRequiredService<IQdosIntakeQueries>().GetAsync(id, CancellationToken.None));
    }

    private static async Task AssertNoBusinessPersistenceAsync(QdosWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            foreach (var table in new[]
                     {
                         "QdosIntakeReceipts",
                         "QdosIntakeAssets",
                         "QdosTypedDrafts",
                         "AuditEvents"
                     })
            {
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM [{table}]";
                Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync(),
                    System.Globalization.CultureInfo.InvariantCulture));
            }

            await using var obsoleteTables = context.Database.GetDbConnection().CreateCommand();
            obsoleteTables.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Cases', 'PrincipalYearCounters')";
            Assert.Equal(0L, Convert.ToInt64(await obsoleteTables.ExecuteScalarAsync(),
                System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
