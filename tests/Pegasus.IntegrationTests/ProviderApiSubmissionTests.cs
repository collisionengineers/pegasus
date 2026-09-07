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
using Pegasus.Core.Workflow;
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
        // ended in "A" — one run in sixteen, not one in sixty-four: the last
        // character of the 43-character base64url tail carries only 4 bits, so
        // it has 16 possible values. That authenticated and failed this
        // assertion for no reason a reader could see.
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
    public async Task AcceptRecoveryRepairsTheSqlCandidateAfterAnInterruptedAccept()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using var created = await SubmitAsync(
            client,
            secret,
            "recovery-1",
            [("note.pdf", "application/pdf", "not a PDF"u8.ToArray())]);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = (await ReadJsonAsync(created)).GetProperty("submissionId").GetGuid();

        await using var scope = api.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var oldReceivedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
            Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ProviderSubmissions SET StagedReceiptId = NULL, ReceivedAtUtc = {oldReceivedAtUtc} WHERE Id = {submissionId}"));
            Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ActionHistory WHERE AggregateType = {ProviderSubmissionPolicy.ActionHistoryAggregateType} AND AggregateId = {submissionId:D} AND Outcome = {"Accepted"}"));
        }

        var result = await scope.ServiceProvider
            .GetRequiredService<ReconcileProviderSubmissions>()
            .ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal(0, result.Failures);

        await using var verification = await contextFactory.CreateDbContextAsync();
        var submission = await verification.ProviderSubmissions
            .AsNoTracking()
            .SingleAsync(item => item.Id == submissionId);
        Assert.NotNull(submission.StagedReceiptId);
        var history = await verification.ActionHistory
            .AsNoTracking()
            .Where(item => item.AggregateType == ProviderSubmissionPolicy.ActionHistoryAggregateType
                && item.AggregateId == submissionId.ToString("D"))
            .ToListAsync();
        var accepted = Assert.Single(history);
        Assert.Equal("Accepted", accepted.Outcome);
        Assert.Equal(ProviderSubmissionPolicy.OperationKey(submissionId), accepted.CorrelationId);
    }

    /// <summary>
    /// A bare reservation — a submission row whose intake retention never
    /// happened — can never be repaired here: nothing removes it, and only a
    /// same-key retry can complete it. It must therefore never occupy the
    /// bounded candidate window, or one storage outage's worth of them
    /// permanently starves every genuinely repairable submission behind them.
    /// </summary>
    [Fact]
    public async Task AcceptRecoveryIsNotStarvedByOlderBareReservations()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using var created = await SubmitAsync(
            client,
            secret,
            "starved-1",
            [("note.pdf", "application/pdf", "not a PDF"u8.ToArray())]);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = (await ReadJsonAsync(created)).GetProperty("submissionId").GetGuid();

        await using var scope = api.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var interruptedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ProviderSubmissions SET StagedReceiptId = NULL, ReceivedAtUtc = {interruptedAtUtc} WHERE Id = {submissionId}"));
            Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ActionHistory WHERE AggregateType = {ProviderSubmissionPolicy.ActionHistoryAggregateType} AND AggregateId = {submissionId:D} AND Outcome = {"Accepted"}"));

            // A day older than the interrupted accept, so oldest-first
            // ordering puts every one of them ahead of it.
            var template = await context.ProviderSubmissions
                .AsNoTracking()
                .SingleAsync(item => item.Id == submissionId);
            for (var index = 0; index < 60; index++)
            {
                context.ProviderSubmissions.Add(new ProviderSubmissionEntity
                {
                    Id = Guid.NewGuid(),
                    PrincipalId = template.PrincipalId,
                    KeyId = template.KeyId,
                    IdempotencyKey = $"bare-reservation-{index}",
                    ProviderReference = template.ProviderReference,
                    ReceivedAtUtc = interruptedAtUtc.AddDays(-1),
                    DeclaredInstructionJson = template.DeclaredInstructionJson
                });
            }

            await context.SaveChangesAsync();
        }

        var result = await scope.ServiceProvider
            .GetRequiredService<ReconcileProviderSubmissions>()
            .ExecuteAsync(50, CancellationToken.None);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Repaired);
        Assert.Equal(0, result.Failures);
        await using var verification = await contextFactory.CreateDbContextAsync();
        var submission = await verification.ProviderSubmissions
            .AsNoTracking()
            .SingleAsync(item => item.Id == submissionId);
        Assert.NotNull(submission.StagedReceiptId);
        var accepted = await verification.ActionHistory
            .AsNoTracking()
            .SingleAsync(item => item.AggregateType == ProviderSubmissionPolicy.ActionHistoryAggregateType
                && item.AggregateId == submissionId.ToString("D"));
        Assert.Equal("Accepted", accepted.Outcome);
    }

    /// <summary>
    /// What stops two Accepted rows reaching permanent history is the derived
    /// identity, so it has to be the database refusing the second write rather
    /// than a time window nobody can hold.
    /// </summary>
    [Fact]
    public async Task ASecondAcceptedRowForOneSubmissionIsRefused()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using var created = await SubmitAsync(
            client,
            secret,
            "duplicate-accept-1",
            [("note.pdf", "application/pdf", "not a PDF"u8.ToArray())]);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submissionId = (await ReadJsonAsync(created)).GetProperty("submissionId").GetGuid();

        await using var scope = api.Services.CreateAsyncScope();
        var principalId = await QdosPrincipalIdAsync(scope.ServiceProvider);
        // Exactly what accept recovery would append for this submission,
        // arriving after the request appended its own row.
        var written = await scope.ServiceProvider.GetRequiredService<IActionHistoryWriter>().TryAppendAsync(
            new(
                ProviderSubmissionPolicy.AcceptedHistoryId(submissionId),
                ProviderSubmissionPolicy.ActionHistoryAggregateType,
                submissionId.ToString("D"),
                "Submitted",
                ActionActor.Provider(principalId),
                DateTimeOffset.UtcNow,
                "Accepted",
                ProviderSubmissionPolicy.OperationKey(submissionId)),
            CancellationToken.None);

        Assert.False(written);
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var accepted = await context.ActionHistory
            .AsNoTracking()
            .SingleAsync(item => item.AggregateType == ProviderSubmissionPolicy.ActionHistoryAggregateType
                && item.AggregateId == submissionId.ToString("D")
                && item.Outcome == "Accepted");
        // The row that stands is the request's own, with the request's
        // correlation id rather than the recovery operation key.
        Assert.NotEqual(ProviderSubmissionPolicy.OperationKey(submissionId), accepted.CorrelationId);
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
    public async Task PausedCredentialIsRefusedBeforeTheBodyIsParsed()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        await PauseQdosCredentialAsync(api);

        // A plain 403 assertion on a valid body also passes on the unfixed code.
        // Malformed JSON makes this go 400 if the guard moves below the read and
        // parse. This pins refusal ahead of parsing; the adjacent read and parse
        // in SubmitAsync remain proven by inspection.
        using var request = new HttpRequestMessage(HttpMethod.Post, Submissions)
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        request.Headers.Add("Idempotency-Key", "paused-malformed-1");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EnvelopeOverTheProviderFileBoundIs413AndAnotherPrincipalNeverSeesTheSubmission()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using (var oversize = await SubmitAsync(
                   client, secret, "order-4",
                   [("big.pdf", "application/pdf", new byte[IntakeEnvelopeLimits.MaximumProviderApiFileLength + 1])]))
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
        Assert.NotNull(projection.Provider.WorkProviderCode.Current);
        var workProviderCode = projection.Provider.WorkProviderCode.Current!;
        Assert.Equal(QdosPrincipal.Code, workProviderCode.Value);
        Assert.Equal(CaseDataSourceKind.ProviderApi, workProviderCode.Source.Kind);
        Assert.Equal(ProviderInstructionPolicy.PolicyKey, workProviderCode.Source.PolicyKey);
        Assert.Equal(ProviderInstructionPolicy.PolicyVersion, workProviderCode.Source.PolicyVersion);
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

    /// <summary>
    /// H13: API-01 is create-only. A second declared instruction naming the
    /// same claim as an existing Case is durably received (201, its own
    /// submission id, its staged receipt retained), then terminates in
    /// processing under provider_existing_case_match with no evaluation, no
    /// second Case, no PO and no new Case association.
    /// </summary>
    [Fact]
    public async Task ASubmissionMatchingAnExistingCaseIsRejectedWithoutMutationOrDuplicateAllocation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        // Statement 1: the first, unmatched submission creates exactly one Case
        // and exactly one link.
        using var first = await SubmitAsync(
            client,
            secret,
            "existing-case-1",
            [("instruction.pdf", "application/pdf", "not a PDF"u8.ToArray())]);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await ReadJsonAsync(first)).GetProperty("submissionId").GetGuid();
        await DrainAsync(api, firstId);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.Equal(1, await context.Cases.CountAsync());
            Assert.Equal(1, await context.CaseIntakeLinks.CountAsync());
        }

        // Statement 2: HTTP acceptance is unchanged for the duplicate - the
        // envelope is durably received first, exactly as FRD-09 requires.
        using var repeated = await SubmitAsync(
            client,
            secret,
            "existing-case-2",
            [("instruction.pdf", "application/pdf", "not a PDF"u8.ToArray())]);
        Assert.Equal(HttpStatusCode.Created, repeated.StatusCode);
        var repeatedId = (await ReadJsonAsync(repeated)).GetProperty("submissionId").GetGuid();
        Assert.NotEqual(firstId, repeatedId);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;

            // Statement 3: the retained staged receipt exists and is untouched.
            var staged = await StagedReceiptAsync(services, repeatedId);

            // Statement 4: processing terminates as Failed with no evaluation -
            // so no review fields, no draft and no allocation attempt.
            var (status, evaluation) = await DrainStagedToTerminalAsync(services, staged.Id);
            Assert.Equal(QueuedIntakeStatusKind.Failed, status.Status);
            Assert.Equal(ProviderExistingCaseMatchException.FailureCode, status.FailureCode);
            Assert.Null(evaluation);
        }

        // Statement 5: the provider-visible result names the code.
        using (var refused = await SendAsync(
            client,
            HttpMethod.Get,
            $"{Submissions}/{repeatedId:D}",
            secret))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            var result = await ReadJsonAsync(refused);
            Assert.Equal("Failed", result.GetProperty("status").GetString());
            Assert.Equal(
                ProviderExistingCaseMatchException.FailureCode,
                result.GetProperty("failureCode").GetString());
            Assert.Equal(JsonValueKind.Null, result.GetProperty("caseReference").ValueKind);
        }

        // Statements 6 and 7: no duplicate allocation, no new link, no mutation
        // of the matched Case.
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.Equal(1, await context.Cases.CountAsync());
            Assert.Equal(1, await context.CaseIntakeLinks.CountAsync());
        }
    }

    /// <summary>
    /// Statement 8: an AMBIGUOUS match takes the identical path. PR 646 never
    /// drove this branch through the API - it was proven only by shared code -
    /// so it is executed here.
    ///
    /// Ambiguity needs two candidates that BOTH survive the eliminator, and the
    /// eliminator is contradiction-driven: a declared claim reference that
    /// differs from a candidate's ELIMINATES that candidate rather than adding
    /// a second one. Two cases carrying different claim references are
    /// therefore never ambiguous - each is eliminated in turn and the outcome
    /// is NoMatch. The real ambiguity is a duplicate: two cases indexed on the
    /// SAME identity, which is what this fixture seeds.
    /// </summary>
    [Fact]
    public async Task AnAmbiguousExistingCaseMatchIsRejectedOnTheSamePath()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using var created = await SubmitAsync(
            client,
            secret,
            "ambiguous-1",
            [("instruction.pdf", "application/pdf", "not a PDF"u8.ToArray())],
            claimNumber: "12345/1");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await DrainAsync(api, (await ReadJsonAsync(created)).GetProperty("submissionId").GetGuid());

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.Equal(1, await context.Cases.CountAsync());

            // A duplicate of the case just created: the same provider, the same
            // claim token, the same vehicle and the same claimant. Nothing about
            // the next declaration can tell them apart, which is exactly the
            // ambiguity the rejection exists for.
            var template = await context.Cases.AsNoTracking().SingleAsync();
            var indexed = await context.Set<CaseMatchIndexEntity>().AsNoTracking().SingleAsync();
            var duplicateId = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({duplicateId}, {template.PrincipalId}, {template.SequenceLineageId}, {template.Year}, {template.Sequence + 1}, {"QDOS29999"}, {template.Type}, {template.InitialState}, {template.CustodyState}, {template.OriginIntakeReceiptId}, {template.InstructionComplete}, {template.ImagesComplete}, {template.InstructionConfirmedByStaff}, {template.ImagesConfirmedByStaff}, {template.CreatedAtUtc}, {0L}, {Guid.NewGuid()})");

            // CaseWorkflows.State is the CaseLifecycleState enum name; the
            // candidate query joins this row and parses it. It is deliberately
            // NOT CreatedInError, which would send the candidate through the
            // replacement redirect instead of leaving it to survive.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({duplicateId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
            context.Set<CaseMatchIndexEntity>().Add(new CaseMatchIndexEntity
            {
                CaseId = duplicateId,
                WorkProviderCode = indexed.WorkProviderCode,
                DurableClaimToken = indexed.DurableClaimToken,
                NormalizedVrm = indexed.NormalizedVrm,
                NormalizedSurname = indexed.NormalizedSurname,
                NormalizedFirstInitial = indexed.NormalizedFirstInitial,
                IncidentDate = indexed.IncidentDate,
                MatchPolicyKey = indexed.MatchPolicyKey,
                MatchPolicyVersion = indexed.MatchPolicyVersion,
                UpdatedAtUtc = indexed.UpdatedAtUtc
            });
            await context.SaveChangesAsync();
        }

        // The same declared facts again: both indexed cases hit on every key and
        // neither is contradicted, so two candidates survive.
        using var ambiguous = await SubmitAsync(
            client,
            secret,
            "ambiguous-2",
            [("instruction.pdf", "application/pdf", "not a PDF"u8.ToArray())],
            claimNumber: "12345/1");
        Assert.Equal(HttpStatusCode.Created, ambiguous.StatusCode);
        var ambiguousId = (await ReadJsonAsync(ambiguous)).GetProperty("submissionId").GetGuid();

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var staged = await StagedReceiptAsync(services, ambiguousId);
            var (status, evaluation) = await DrainStagedToTerminalAsync(services, staged.Id);
            Assert.Equal(QueuedIntakeStatusKind.Failed, status.Status);
            Assert.Equal(ProviderExistingCaseMatchException.FailureCode, status.FailureCode);
            Assert.Null(evaluation);
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();

            // Nothing was allocated: the two cases are the one created and the
            // one seeded, and the only link is the first submission's.
            Assert.Equal(2, await context.Cases.CountAsync());
            Assert.Equal(1, await context.CaseIntakeLinks.CountAsync());
        }
    }

    /// <summary>
    /// The other half of the same rule, and the reason the fixture above seeds a
    /// duplicate rather than a second distinct case: a declared claim reference
    /// that CONTRADICTS the only candidate eliminates it, so the submission is
    /// an ordinary create-only one and a second case is allocated.
    /// </summary>
    [Fact]
    public async Task ASubmissionContradictingTheExistingCaseCreatesItsOwnCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var api = WithProviderApi(factory);
        using var client = CreateClient(api);
        var secret = await IssueQdosCredentialAsync(api);

        using var first = await SubmitAsync(
            client,
            secret,
            "contradicting-1",
            [("instruction.pdf", "application/pdf", "not a PDF"u8.ToArray())],
            claimNumber: "12345/1");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await DrainAsync(api, (await ReadJsonAsync(first)).GetProperty("submissionId").GetGuid());

        using var second = await SubmitAsync(
            client,
            secret,
            "contradicting-2",
            [("instruction.pdf", "application/pdf", "not a PDF"u8.ToArray())],
            claimNumber: "12345/2");
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        await DrainAsync(api, (await ReadJsonAsync(second)).GetProperty("submissionId").GetGuid());

        await using var scope = api.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(2, await context.Cases.CountAsync());
        Assert.Equal(2, await context.CaseIntakeLinks.CountAsync());
    }

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
        var staged = await StagedReceiptAsync(services, submissionId);
        _ = await IntakeWebDriver.DrainStagedAsync(services, staged.Id);
    }

    private static async Task<IntakeStagedReceipt> StagedReceiptAsync(
        IServiceProvider services,
        Guid submissionId) =>
        await services.GetRequiredService<IIntakeWorkStore>().FindBySourceIdentityAsync(
            new(IntakeSourceChannel.ProviderApi, ProviderSubmissionPolicy.SubmissionToken(submissionId)),
            CancellationToken.None)
            ?? throw new InvalidOperationException("The submission was not retained as a staged receipt.");

    /// <summary>
    /// Pumps a staged receipt until its queued status is terminal - Complete OR
    /// Failed - and returns that status with the (possibly null) completed
    /// evaluation. <see cref="IntakeWebDriver.DrainStagedAsync"/> waits for an
    /// evaluation that a terminal input failure never produces, so it loops for
    /// ever on this shape. Mirrors that method's dispatch-then-late-dispatch
    /// retry so a frozen test clock cannot stall a backoff. Private to this
    /// class: the shared helper is Stream-A-owned test support.
    /// </summary>
    private static async Task<(QueuedIntakeStatus Status, IntakeEvaluationRevision? Evaluation)>
        DrainStagedToTerminalAsync(IServiceProvider services, Guid stagedReceiptId)
    {
        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var statuses = services.GetRequiredService<IQueuedIntakeStatusQueries>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        IIntakeWorkEnqueuer Enqueuer() =>
            new IntakeWebDriver.ImmediateIntakeWorkEnqueuer(IntakeWebDriver.CreateProcessor(services));

        while (true)
        {
            var status = await statuses.GetAsync(stagedReceiptId, CancellationToken.None)
                ?? throw new InvalidOperationException("The staged receipt has no queued status.");
            if (status.Status is QueuedIntakeStatusKind.Complete or QueuedIntakeStatusKind.Failed)
            {
                return (
                    status,
                    await workStore.GetCompletedEvaluationAsync(stagedReceiptId, CancellationToken.None));
            }

            var dispatched = await new DispatchPendingIntakeWork(workStore, Enqueuer(), timeProvider)
                .ExecuteAsync(1, CancellationToken.None);
            if (dispatched == 0)
            {
                dispatched = await new DispatchPendingIntakeWork(
                        workStore,
                        Enqueuer(),
                        new LateTimeProvider(timeProvider, TimeSpan.FromMinutes(10)))
                    .ExecuteAsync(1, CancellationToken.None);
            }

            Assert.Equal(1, dispatched);
        }
    }

    private sealed class LateTimeProvider(TimeProvider inner, TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow() + offset;
    }
}
