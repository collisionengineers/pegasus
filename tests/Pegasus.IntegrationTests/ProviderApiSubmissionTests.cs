using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.ProviderApi;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// API-01 through the composed Web host: the dedicated bearer scheme, the
/// Principal-bound intake path, idempotent replay, pause semantics and the
/// envelope bound. Processing is drained in-process exactly as the other
/// intake tests do, standing in for the Worker.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ProviderApiSubmissionTests
{
    private const string Submissions = "/api/provider/v1/submissions";
    private static readonly ActionActor Administrator = ActionActor.Staff(
        Guid.Parse("0f149cac-e1d4-4a57-925f-7c35d33d7f5b"),
        [StaffRole.Administrator]);

    private static WebApplicationFactory<Program> WithProviderApi(IntakeWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("Features:ProviderApi", "true"));

    [Fact]
    public async Task SurfaceIsAbsentUntilComposed()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.PostAsync(Submissions, new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RefusedCredentialsAre401WithASecurityEventAndNeverASignInRedirect()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using (var anonymous = await client.GetAsync($"{Submissions}/{Guid.NewGuid():D}"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.Contains("pegasus-provider-api", anonymous.Headers.WwwAuthenticate.ToString());
            Assert.Equal("application/problem+json", anonymous.Content.Headers.ContentType?.MediaType);
        }

        using (var wrong = await SendAsync(client, HttpMethod.Get, $"{Submissions}/{Guid.NewGuid():D}", secret[..^1] + "A"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.DoesNotContain(secret, await wrong.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'provider_credential_missing' AND Outcome = N'Denied'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'provider_credential_rejected' AND Outcome = N'Denied'
              AND SubjectId = N'{secret.Substring(4, 16)}'
            """));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM SecurityEvents WHERE SubjectId LIKE N'%{secret}%'"));
    }

    [Fact]
    public async Task SubmissionIsReceivedBoundToThePrincipalAndResolvesToACaseAfterProcessing()
    {
        // The typed classification double stands in for the generated QDOS
        // document tells, exactly as the allocation-recovery tests do; the
        // route, extraction, allocation and action-history paths are real.
        // The host runs on the system clock, not the suite's fixed one: this
        // test asserts the recorded order of three history entries by their
        // own OccurredAtUtc, which a pinned clock would collapse into a tie.
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            TimeProvider.System,
            mailClassificationPolicy: new ConsumerTypedClassificationPolicy());
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);
        // A forwarded instruction whose sender is not a QDOS route: only the
        // credential binds it to QDOS.
        var email = IntakeTestEvidence.CreateEmail(
            "instruction.eml",
            "QDOS instruction\r\nClaimant Name: Provider Claimant\r\nClaim Number: PROV-001\r\nVehicle Registration: AB12 CDE",
            senderAddress: "intermediary@example.test");

        using var created = await SubmitAsync(client, secret, "order-1", [("instruction.eml", email.MediaType, email.Content)], "PROV-001");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var receipt = await ReadJsonAsync(created);
        var submissionId = receipt.GetProperty("submissionId").GetGuid();
        Assert.Equal($"{Submissions}/{submissionId:D}", created.Headers.Location?.OriginalString);
        Assert.False(receipt.GetProperty("replayed").GetBoolean());
        Assert.Equal("PROV-001", receipt.GetProperty("providerReference").GetString());
        var file = Assert.Single(receipt.GetProperty("files").EnumerateArray());
        Assert.Equal("instruction.eml", file.GetProperty("fileName").GetString());
        Assert.False(file.GetProperty("duplicate").GetBoolean());

        using (var pending = await SendAsync(client, HttpMethod.Get, $"{Submissions}/{submissionId:D}", secret))
        {
            Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
            var result = await ReadJsonAsync(pending);
            Assert.Equal("Received", result.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, result.GetProperty("caseReference").ValueKind);
        }

        using (var replay = await SubmitAsync(client, secret, "order-1", [("instruction.eml", email.MediaType, email.Content)]))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            var replayed = await ReadJsonAsync(replay);
            Assert.Equal(submissionId, replayed.GetProperty("submissionId").GetGuid());
            Assert.True(replayed.GetProperty("replayed").GetBoolean());
        }

        using (var conflict = await SubmitAsync(client, secret, "order-1", [("other.eml", email.MediaType, [1, 2, 3])]))
        {
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        }

        await DrainAsync(api, submissionId);

        using (var complete = await SendAsync(client, HttpMethod.Get, $"{Submissions}/{submissionId:D}", secret))
        {
            Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
            var result = await ReadJsonAsync(complete);
            Assert.Equal("Complete", result.GetProperty("status").GetString());
            var caseReference = result.GetProperty("caseReference").GetString();
            Assert.False(string.IsNullOrWhiteSpace(caseReference));
            var processed = Assert.Single(result.GetProperty("files").EnumerateArray());
            Assert.Equal("CaseCreated", processed.GetProperty("decision").GetString());
            Assert.Equal(caseReference, processed.GetProperty("caseReference").GetString());
        }

        await using var scope = api.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var principalId = await context.Principals.Where(item => item.Code == QdosPrincipal.Code)
            .Select(item => item.Id).SingleAsync();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == ProviderSubmissionPolicy.ActionHistoryAggregateType)
            .OrderBy(item => item.OccurredAtUtc)
            .ToListAsync();
        Assert.Equal(["Accepted", "Replayed", "Refused"], history.Select(item => item.Outcome));
        Assert.All(history, item =>
        {
            Assert.Equal(nameof(ActorKind.Provider), item.ActorKind);
            Assert.Equal(principalId.ToString("D"), item.ActorSubjectId);
        });
        var staged = await context.IntakeStagedReceipts.AsNoTracking().SingleAsync();
        Assert.Equal("provider_api", staged.SourceChannel);
        Assert.Equal($"provider:{principalId:D}", staged.Actor);
    }

    [Fact]
    public async Task PausedCredentialIsRefusedForSubmissionAndStillReadsItsOwnResult()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);
        using var created = await SubmitAsync(client, secret, "order-2", [("note.pdf", "application/pdf", "not a PDF"u8.ToArray())]);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = (await ReadJsonAsync(created)).GetProperty("submissionId").GetGuid();

        await PauseQdosCredentialAsync(api);

        using (var refused = await SubmitAsync(client, secret, "order-3", [("note.pdf", "application/pdf", "not a PDF"u8.ToArray())]))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }
        using (var read = await SendAsync(client, HttpMethod.Get, $"{Submissions}/{submissionId:D}", secret))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'provider_credential_paused' AND Outcome = N'Denied'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM ProviderSubmissions"));
    }

    [Fact]
    public async Task EnvelopeOverTheUploadLimitIs413AndAnotherPrincipalNeverSeesTheSubmission()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using (var oversize = await SubmitAsync(
                   client, secret, "order-4",
                   [("big.pdf", "application/pdf", new byte[IntakeEnvelopeLimits.MaximumContentLength + 1])]))
        {
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversize.StatusCode);
        }
        using (var missingKey = await SubmitAsync(
                   client, secret, idempotencyKey: null,
                   [("note.pdf", "application/pdf", "not a PDF"u8.ToArray())]))
        {
            Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        }
        Assert.Equal(0, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM ProviderSubmissions"));

        using var created = await SubmitAsync(client, secret, "order-5", [("note.pdf", "application/pdf", "not a PDF"u8.ToArray())]);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = (await ReadJsonAsync(created)).GetProperty("submissionId").GetGuid();

        var otherSecret = await IssueOtherPrincipalCredentialAsync(api);
        using var foreign = await SendAsync(client, HttpMethod.Get, $"{Submissions}/{submissionId:D}", otherSecret);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> api) =>
        api.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

    private static async Task<HttpResponseMessage> SubmitAsync(
        HttpClient client,
        string secret,
        string? idempotencyKey,
        IReadOnlyList<(string Name, string MediaType, byte[] Bytes)> files,
        string? providerReference = null)
    {
        var multipart = new MultipartFormDataContent();
        if (providerReference is not null)
        {
            multipart.Add(new StringContent(providerReference), "providerReference");
        }
        foreach (var (name, mediaType, bytes) in files)
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
            multipart.Add(content, "files", name);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Submissions) { Content = multipart };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string secret)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<Guid> QdosPrincipalIdAsync(IServiceProvider services)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Principals.Where(item => item.Code == QdosPrincipal.Code)
            .Select(item => item.Id).SingleAsync();
    }

    private static async Task<string> IssueQdosCredentialAsync(WebApplicationFactory<Program> api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var principalId = await QdosPrincipalIdAsync(scope.ServiceProvider);
        var issued = await scope.ServiceProvider.GetRequiredService<IIssuePrincipalCredential>().ExecuteAsync(
            new(principalId, 0, Administrator, $"issue:{Guid.NewGuid():N}", "provider api test"),
            default);
        return issued.Secret ?? throw new InvalidOperationException("The issued secret was not returned.");
    }

    private static async Task PauseQdosCredentialAsync(WebApplicationFactory<Program> api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var principalId = await QdosPrincipalIdAsync(scope.ServiceProvider);
        var current = await scope.ServiceProvider.GetRequiredService<IGetPrincipalCredential>()
            .ExecuteAsync(Administrator, principalId, default)
            ?? throw new InvalidOperationException("The credential was not issued.");
        await scope.ServiceProvider.GetRequiredService<IPausePrincipalCredential>().ExecuteAsync(
            new(principalId, current.Version, Administrator, $"pause:{Guid.NewGuid():N}", "provider api test"),
            default);
    }

    private static async Task<string> IssueOtherPrincipalCredentialAsync(WebApplicationFactory<Program> api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var organization = await services.GetRequiredService<ICreateOrganization>().ExecuteAsync(
            new("Other Provider", [OrganizationRole.WorkProvider], Administrator, "provider-api:org:other"),
            default);
        var principal = await services.GetRequiredService<ICreatePrincipal>().ExecuteAsync(
            new(organization.Id, "OTHER", Administrator, "provider-api:principal:other"),
            default);
        var issued = await services.GetRequiredService<IIssuePrincipalCredential>().ExecuteAsync(
            new(principal.Id, 0, Administrator, "provider-api:issue:other", "provider api test"),
            default);
        return issued.Secret ?? throw new InvalidOperationException("The issued secret was not returned.");
    }

    /// <summary>
    /// Drains every member of the submission's intake group, standing in for
    /// the Worker timer and queue trigger.
    /// </summary>
    private static async Task DrainAsync(WebApplicationFactory<Program> api, Guid submissionId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var group = await services.GetRequiredService<IIntakeSubmissionGroupStore>().FindAsync(
            IntakeSourceChannel.ProviderApi,
            ProviderSubmissionPolicy.SubmissionToken(submissionId))
            ?? throw new InvalidOperationException("The submission group was not persisted.");
        foreach (var member in group.Members.OrderBy(member => member.Ordinal))
        {
            _ = await IntakeWebDriver.DrainStagedAsync(services, member.StagedReceiptId);
        }
    }
}
