using System.Net;
using System.Net.Http.Headers;
using System.Text;
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
using Pegasus.Web.Presentation;

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

        using var response = await client.PostAsync(
            Submissions,
            new StringContent("{}", Encoding.UTF8, "application/json"));

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

        // Change the last character to one it is not. Appending a fixed "A"
        // silently produced the *same* secret whenever the issued one already
        // ended in "A" — roughly one run in sixty-four, which authenticated and
        // failed this assertion for no reason a reader could see.
        var wrongSecret = secret[..^1] + (secret[^1] == 'A' ? 'B' : 'A');
        using (var wrong = await SendAsync(client, HttpMethod.Get, $"{Submissions}/{Guid.NewGuid():D}", wrongSecret))
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

        using var created = await SubmitAsync(
            client, secret, "order-1", [("instruction.eml", email.MediaType, email.Content)], "PROV-001");
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

        // The same key with the same body: a replay, not a new submission.
        using (var replay = await SubmitAsync(
                   client, secret, "order-1", [("instruction.eml", email.MediaType, email.Content)], "PROV-001"))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            var replayed = await ReadJsonAsync(replay);
            Assert.Equal(submissionId, replayed.GetProperty("submissionId").GetGuid());
            Assert.True(replayed.GetProperty("replayed").GetBoolean());
        }

        // The same key with a different body: refused, and nothing new retained.
        using (var conflict = await SubmitAsync(
                   client, secret, "order-1", [("other.eml", email.MediaType, [1, 2, 3])], "PROV-001"))
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
            Assert.Equal("CaseCreated", result.GetProperty("decision").GetString());
            // The declared claimant and registration reached the case, and the
            // reference belongs to the Principal the credential authenticated.
            Assert.StartsWith(QdosPrincipal.Code, caseReference, StringComparison.Ordinal);
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
        // One submission, one receipt: the request as sent, carrying its files
        // as attachments rather than scattering them across receipts.
        Assert.Equal(ProviderInstructionPolicy.SourceFileName, staged.SourceFileName);
        Assert.Equal(submissionId.ToString("N"), staged.ExternalReceiptToken);
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

    [Fact]
    public async Task ADeclaredAuditTakesItsReferencePrefixFromTheDeclaredVerdict()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true, TimeProvider.System);
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using var created = await SubmitAsync(
            client,
            secret,
            "audit-1",
            [
                ("instruction.pdf", "application/pdf", "instruction"u8.ToArray()),
                ("original-report.pdf", "application/pdf", "report"u8.ToArray())
            ],
            caseType: "audit",
            originalReportVerdict: "total-loss",
            fileRole: "originalreport");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = (await ReadJsonAsync(created)).GetProperty("submissionId").GetGuid();

        await DrainAsync(api, submissionId);

        using var complete = await SendAsync(client, HttpMethod.Get, $"{Submissions}/{submissionId:D}", secret);
        var result = await ReadJsonAsync(complete);
        var caseReference = result.GetProperty("caseReference").GetString();
        // The operator ruled on 2026-08-28 that the declared verdict decides the
        // reference; `total loss` derives the ap. prefix (FRD-01).
        Assert.StartsWith("ap.", caseReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeclaredTriageOpensATriageAndAllocatesNoCase()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true, TimeProvider.System);
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using var created = await SubmitAsync(
            client, secret, "triage-1",
            [("request.pdf", "application/pdf", "triage"u8.ToArray())],
            caseType: "triage");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = (await ReadJsonAsync(created)).GetProperty("submissionId").GetGuid();

        await DrainAsync(api, submissionId);

        using var complete = await SendAsync(client, HttpMethod.Get, $"{Submissions}/{submissionId:D}", secret);
        var result = await ReadJsonAsync(complete);
        // Triage is pre-case work: it opens a Triage record and allocates no
        // Case/PO (FRD-03).
        Assert.Equal(JsonValueKind.Null, result.GetProperty("caseReference").ValueKind);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM Triage"));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM Cases"));
        // The Triage is the destination. Without this the same material also
        // sits in the Unidentified queue, which is the two-queues defect
        // INTK-033 closed for the mail route.
        Assert.Equal(0, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM UnidentifiedItems"));
    }

    [Fact]
    public async Task ABodyNamingAnotherPrincipalIs403AndAMalformedFieldIs400()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using (var mismatch = await SubmitAsync(
                   client, secret, "mismatch-1",
                   [("note.pdf", "application/pdf", "x"u8.ToArray())],
                   principal: "SOMEONE-ELSE"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, mismatch.StatusCode);
        }

        using (var badType = await SubmitAsync(
                   client, secret, "bad-1",
                   [("note.pdf", "application/pdf", "x"u8.ToArray())],
                   caseType: "not-a-case-type"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        }

        // Nothing was retained by either refusal.
        Assert.Equal(0, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM ProviderSubmissions"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'provider_principal_mismatch' AND Outcome = N'Denied'
            """));
    }

    [Fact]
    public async Task AProviderCreatedCaseReadsItsDataSnapshotBack()
    {
        // The snapshot records the origin channel exactly as the receipt wrote
        // it — "provider_api". Reading it back is the path the EVA send page
        // and the assessment tools take through ICaseDataQueries, so a reader
        // that does not know the channel fails the case after allocation
        // rather than at submission, where nothing would have been retained.
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            TimeProvider.System,
            mailClassificationPolicy: new ConsumerTypedClassificationPolicy());
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);
        var email = IntakeTestEvidence.CreateEmail(
            "instruction.eml",
            "QDOS instruction\r\nClaimant Name: Provider Claimant\r\nClaim Number: PROV-002\r\nVehicle Registration: AB12 CDE",
            senderAddress: "intermediary@example.test");

        using var created = await SubmitAsync(
            client, secret, "order-2", [("instruction.eml", email.MediaType, email.Content)], "PROV-002");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = (await ReadJsonAsync(created)).GetProperty("submissionId").GetGuid();

        await DrainAsync(api, submissionId);

        string caseReference;
        using (var complete = await SendAsync(client, HttpMethod.Get, $"{Submissions}/{submissionId:D}", secret))
        {
            Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
            var result = await ReadJsonAsync(complete);
            Assert.Equal("CaseCreated", result.GetProperty("decision").GetString());
            caseReference = result.GetProperty("caseReference").GetString()
                ?? throw new InvalidOperationException(
                    "The completed submission carried no case reference.");
        }

        await using var scope = api.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var caseId = await context.Cases
            .Where(item => item.Reference == caseReference)
            .Select(item => item.Id)
            .SingleAsync();

        var projection = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CancellationToken.None);

        Assert.NotNull(projection);
        Assert.Equal(IntakeSourceChannel.ProviderApi, projection.Origin.Channel);
        Assert.Equal(ProviderInstructionPolicy.ReaderKey, projection.Origin.SourceReaderKey);
        Assert.Equal(ProviderInstructionPolicy.ReaderVersion, projection.Origin.SourceReaderVersion);
        Assert.Equal(
            OperatorLabels.ProviderSubmissionApi.Source,
            OperatorLabels.SourceChannel(projection.Origin.Channel));
        Assert.Equal(
            OperatorLabels.ProviderSubmissionApi.Source,
            OperatorLabels.SourceChannel("provider_api"));
        Assert.NotNull(projection.Claimant.Name.Current);
        var claimantName = projection.Claimant.Name.Current!;
        Assert.Equal(CaseDataSourceKind.ProviderApi, claimantName.Source.Kind);
        Assert.Equal(
            (
                OperatorLabels.ProviderSubmissionApi.Source,
                OperatorLabels.ProviderSubmissionApi.ProvenanceIcon),
            OperatorLabels.Provenance(claimantName.Source));
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
        string claimNumber = "12345/1",
        string caseType = "inspection",
        string? principal = null,
        string? originalReportVerdict = null,
        string? fileRole = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["principal"] = principal,
            ["claimNumber"] = claimNumber,
            ["caseType"] = caseType,
            ["originalReportVerdict"] = originalReportVerdict,
            ["claimant"] = new Dictionary<string, object?> { ["name"] = "Alex Mercer" },
            ["vehicle"] = new Dictionary<string, object?> { ["registration"] = "AB12CDE" },
            ["files"] = files
                .Select((file, index) => new Dictionary<string, object?>
                {
                    ["ordinal"] = index,
                    ["fileName"] = file.Name,
                    ["mediaType"] = file.MediaType,
                    ["role"] = index == 0 ? null : fileRole,
                    ["contentBase64"] = Convert.ToBase64String(file.Bytes)
                })
                .ToArray()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Submissions)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
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
    /// Drains the submission's one staged receipt, standing in for the Worker
    /// timer and queue trigger. One submission is one receipt: its files are
    /// that receipt's attachments, not receipts of their own.
    /// </summary>
    private static async Task DrainAsync(WebApplicationFactory<Program> api, Guid submissionId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var staged = await services.GetRequiredService<IIntakeWorkStore>().FindBySourceIdentityAsync(
            new(IntakeSourceChannel.ProviderApi, ProviderSubmissionPolicy.SubmissionToken(submissionId)),
            CancellationToken.None)
            ?? throw new InvalidOperationException("The submission was not retained as a staged receipt.");
        _ = await IntakeWebDriver.DrainStagedAsync(services, staged.Id);
    }
}
