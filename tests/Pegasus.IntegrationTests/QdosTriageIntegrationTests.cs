using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class QdosTriageIntegrationTests
{
    private const string AcceptedMatcherKey = "integration-test-accepted-triage-matcher";
    private const long SeededCaseEntityVersion = 37;

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task AcceptedTriageMatchEvidenceCreatesOneReplaySafeTriage()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-request.eml",
            "QDOS instruction\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-001\r\nVehicle Registration: AB12 CDE");
        const string replayToken = "77777777777777777777777777777777";

        var first = await IntakeWebDriver.UploadAndProcessAsync(factory, client, email.FileName,
        email.MediaType,
        email.Content,
        replayToken);
        var replay = await IntakeWebDriver.UploadAndProcessAsync(factory, client, email.FileName,
        email.MediaType,
        email.Content,
        replayToken);
        var receiptId = IntakeWebDriver.ReceiptId(first);

        Assert.Equal(receiptId, IntakeWebDriver.ReceiptId(replay));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.IsType<CreateTriageFromIntake>(
            scope.ServiceProvider.GetRequiredService<ICreateTriageFromIntake>());
        var triageQueries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        var summary = Assert.Single(
            await triageQueries.ListAsync(null, CancellationToken.None));
        var detail = Assert.IsType<TriageDetail>(
            await triageQueries.GetAsync(summary.Id, CancellationToken.None));
        var evaluation = Assert.Single(
            await GetEvaluationRevisionsAsync(factory.Database, receiptId));

        Assert.Equal(1, evaluation.Revision);
        Assert.Equal(receiptId, detail.Record.Origin.ReceiptId);
        Assert.Equal(evaluation.Id, detail.Record.Origin.EvaluationRevisionId);
        Assert.Equal("AB12CDE", detail.Record.NormalizedVehicleRegistration);
        Assert.Equal(TriageState.Open, detail.Record.State);
        Assert.Null(detail.Record.LinkedCaseId);
        Assert.Empty(detail.Findings);
        Assert.Empty(detail.ResponseEvidence);
        var created = Assert.Single(detail.History);
        Assert.Equal("triage_created", created.EventType);
        Assert.Contains(AcceptedMatcherKey, created.Reason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task CaseCreatedWithoutAcceptedTriageMatchEvidenceDoesNotCreateTriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "ordinary-instruction.eml",
            "QDOS instruction\r\nClaimant Name: Ordinary Claimant\r\nClaim Number: ORDINARY-001\r\nVehicle Registration: AB12 CDE");

        var upload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, email.FileName,
        email.MediaType,
        email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var receipt = Assert.IsType<IntakeReceipt>(
            await receipts.GetAsync(receiptId, CancellationToken.None));
        var triageQueries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();

        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        Assert.Equal("AB12CDE", receipt.InstructionDraft?.VehicleRegistration);
        Assert.DoesNotContain(
            receipt.Evidence,
            evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch);
        Assert.Empty(await triageQueries.ListAsync(null, CancellationToken.None));
        Assert.Single(await GetEvaluationRevisionsAsync(factory.Database, receiptId));
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task MultipleAcceptedTriageMatchesFailClosedWithoutCreatingTriage()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy(matchCount: 2));
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "ambiguous-triage-request.eml",
            "QDOS instruction\r\nClaimant Name: Ambiguous Claimant\r\nClaim Number: AMBIGUOUS-001\r\nVehicle Registration: AB12 CDE");

        var upload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, email.FileName,
        email.MediaType,
        email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(
            await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None));
        Assert.Equal(
            2,
            receipt.Evidence.Count(
                evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch));
        Assert.Empty(
            await scope.ServiceProvider.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task NonQualifyingCompletedIntakePersistsEvaluationWithoutCreatingTriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var needsSorting = Encoding.UTF8.GetBytes(
            "From: unknown@example.test\r\n" +
            "To: intake@example.test\r\n" +
            "Subject: Unclassified correspondence\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
            "This retained correspondence contains no supported instruction evidence.");
        var missingRegistration = IntakeTestEvidence.CreateEmail(
            "missing-registration.eml",
            "QDOS instruction\r\nClaimant Name: No Registration\r\nClaim Number: TRIAGE-002");

        var sortingUpload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, "needs-sorting.eml",
        "message/rfc822",
        needsSorting);
        var missingRegistrationUpload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, missingRegistration.FileName,
        missingRegistration.MediaType,
        missingRegistration.Content);
        var blockedUpload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, "unsupported.txt",
        "text/plain",
        Encoding.UTF8.GetBytes("Unsupported intake source."));
        var sortingReceiptId = IntakeWebDriver.ReceiptId(sortingUpload);
        var missingRegistrationReceiptId = IntakeWebDriver.ReceiptId(missingRegistrationUpload);
        var blockedReceiptId = IntakeWebDriver.ReceiptId(blockedUpload);

        await using var scope = factory.Services.CreateAsyncScope();
        var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var triageQueries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        var sortingReceipt = Assert.IsType<IntakeReceipt>(
            await receipts.GetAsync(sortingReceiptId, CancellationToken.None));
        var missingRegistrationReceipt = Assert.IsType<IntakeReceipt>(
            await receipts.GetAsync(missingRegistrationReceiptId, CancellationToken.None));
        var blockedReceipt = Assert.IsType<IntakeReceipt>(
            await receipts.GetAsync(blockedReceiptId, CancellationToken.None));

        Assert.Equal(IntakeDecision.NeedsSorting, sortingReceipt.Decision);
        Assert.Equal(IntakeDecision.CaseCreated, missingRegistrationReceipt.Decision);
        Assert.Null(missingRegistrationReceipt.InstructionDraft?.VehicleRegistration);
        Assert.Equal(IntakeDecision.Unsupported, blockedReceipt.Decision);
        Assert.Empty(await triageQueries.ListAsync(null, CancellationToken.None));
        Assert.Single(await GetEvaluationRevisionsAsync(factory.Database, sortingReceiptId));
        Assert.Single(await GetEvaluationRevisionsAsync(factory.Database, missingRegistrationReceiptId));
        Assert.Single(await GetEvaluationRevisionsAsync(factory.Database, blockedReceiptId));
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task AuthenticatedTriagePageExecutesLifecycleWithVersionsAndPermanentHistory()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-lifecycle.eml",
            "QDOS instruction\r\nClaimant Name: Lifecycle Claimant\r\nClaim Number: TRIAGE-LIFECYCLE\r\nVehicle Registration: XY12 ZZZ");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, email.FileName,
        email.MediaType,
        email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        var triage = await GetOnlyTriageAsync(factory.Services);
        var triageId = triage.Record.Id;
        var actor = DevelopmentOfflineIdentity.AdministratorId.ToString("D");

        using var detailResponse = await client.GetAsync($"/Triage/{triageId:D}");
        var detailHtml = await detailResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        // The record is one container now: its registration and state are the
        // header, not a "Triage record" panel among stacked panels.
        Assert.Contains("class=\"record\"", detailHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "name=\"caseEditLeaseToken\"",
            detailHtml,
            StringComparison.Ordinal);

        // The record's own identifiers are internal. An operator cannot act on
        // a receipt GUID, an evaluation revision or a source hash, and none of
        // them is printed any more.
        Assert.DoesNotContain("Source SHA-256", detailHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Evaluation revision", detailHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(triage.Record.Origin.SourceHash, detailHtml, StringComparison.Ordinal);

        // Completion keeps its place with its condition named, rather than
        // disappearing until it happens to work.
        Assert.Contains(
            "Available once a finding is recorded",
            detailHtml,
            StringComparison.Ordinal);
        var antiforgeryToken = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);


        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            0,
            "assign",
            "Claimed by the reviewing operator",
            // The engineer is named explicitly now; nothing defaults to the
            // signed-in staff member.
            KeyValuePair.Create(
                "assigneeId",
                DevelopmentOfflineIdentity.AdministratorId.ToString("D")));
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(1, triage.Record.Version);
        Assert.Equal(DevelopmentOfflineIdentity.AdministratorId, triage.Record.AssigneeId);

        var staleHtml = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            0,
            "cancel",
            "Stale cancellation must fail");
        Assert.Contains("not expected version 0", staleHtml, StringComparison.Ordinal);
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(1, triage.Record.Version);
        Assert.Equal(2, triage.History.Count);

        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            1,
            "record_finding",
            "Reviewed assessment",
            KeyValuePair.Create("roadworthiness", nameof(RoadworthinessFinding.Unroadworthy)),
            KeyValuePair.Create("assessment", nameof(AssessmentFinding.TotalLoss)));
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.FindingRecorded, triage.Record.State);
        Assert.Equal(2, triage.Record.Version);
        var initialFinding = Assert.Single(triage.Findings);

        Guid sentEvidenceId;
        var pollOutcomeId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sentRecorder = scope.ServiceProvider.GetRequiredService<IRecordSentEmailEvidence>();
            var sent = await sentRecorder.ExecuteAsync(
                new(
                    triageId,
                    2,
                    "sent-item:triage-lifecycle",
                    "Triage response",
                    ["recipient@example.test"],
                    new string('a', 64),
                    new DateTimeOffset(2031, 5, 6, 11, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2031, 5, 13, 11, 0, 0, TimeSpan.Zero),
                    actor,
                    Guid.NewGuid().ToString("N")),
                CancellationToken.None);
            sentEvidenceId = sent.Id;

            await SeedReplyCandidateAsync(
                factory.Database,
                pollOutcomeId,
                sent.MessageIdentity);
        }

        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(2, triage.Record.Version);
        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            2,
            "link_response",
            "Confirmed exact reply-chain evidence",
            KeyValuePair.Create(
                "responseCandidate",
                $"{pollOutcomeId:D}|{sentEvidenceId:D}"));
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(3, triage.Record.Version);
        Assert.Equal(sentEvidenceId, Assert.Single(triage.ResponseEvidence).SentEvidenceId);

        var completedHtml = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            3,
            "complete",
            "Finding and exact response confirmed");
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.Completed, triage.Record.State);
        Assert.Equal(4, triage.Record.Version);
        Assert.Contains("Post-send correction", completedHtml, StringComparison.Ordinal);

        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            4,
            "supersede_finding",
            "Correction after further retained evidence",
            KeyValuePair.Create("roadworthiness", nameof(RoadworthinessFinding.Roadworthy)),
            KeyValuePair.Create("assessment", nameof(AssessmentFinding.Repairable)),
            KeyValuePair.Create("supersedesFindingId", initialFinding.Id.ToString("D")));
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.FindingRecorded, triage.Record.State);
        Assert.Equal(5, triage.Record.Version);
        Assert.Equal(initialFinding.Id, triage.Findings.Single(
            finding => finding.SupersedesFindingId is not null).SupersedesFindingId);
        Assert.Empty(triage.ResponseEvidence);

        var completionWithoutNewResponse = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            5,
            "complete",
            "Old response evidence must not satisfy a corrected finding");
        Assert.Contains(
            "completion requires exactly one replied Sent email evidence link",
            completionWithoutNewResponse,
            StringComparison.Ordinal);
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(5, triage.Record.Version);

        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            5,
            "cancel",
            "Provider withdrew the request");
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.Cancelled, triage.Record.State);
        Assert.Equal(6, triage.Record.Version);

        var caseId = await SeedCaseAsync(factory.Services, receiptId);
        var otherHolder = ActionActor.Staff(
            Guid.NewGuid(),
            [StaffRole.Administrator]);
        CaseEditLease conflictingLease;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            conflictingLease = await scope.ServiceProvider
                .GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(
                        caseId,
                        0,
                        otherHolder,
                        Guid.NewGuid().ToString("N")),
                    CancellationToken.None);
        }

        var unavailableCaseHtml = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            6,
            "link_case",
            "Must not bypass another holder",
            KeyValuePair.Create("caseId", caseId.ToString("D")));
        // The holder is disclosed by staff account, never by identifier, and the wording and
        // clock are the ones the case workspace uses.
        Assert.Contains(
            "Case locked - ",
            unavailableCaseHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            "is editing the case",
            unavailableCaseHtml,
            StringComparison.Ordinal);
        // CASE-024: an open editor keeps its own lease alive, so this names no time.
        Assert.DoesNotContain(
            "Editing becomes available",
            unavailableCaseHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            otherHolder.SubjectId,
            unavailableCaseHtml,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "value=\"link_case\"",
            unavailableCaseHtml,
            StringComparison.Ordinal);
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Null(triage.Record.LinkedCaseId);
        Assert.Equal(6, triage.Record.Version);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>().ReleaseAsync(
                new(
                    caseId,
                    otherHolder,
                    Guid.NewGuid().ToString("N"),
                    conflictingLease.Token),
                CancellationToken.None);
        }

        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            6,
            "link_case",
            "Associated later instruction",
            KeyValuePair.Create("caseId", caseId.ToString("D")));
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(caseId, triage.Record.LinkedCaseId);
        Assert.Equal(7, triage.Record.Version);
        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            7,
            "unlink_case",
            "Association corrected",
            KeyValuePair.Create("caseId", caseId.ToString("D")));
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Null(triage.Record.LinkedCaseId);
        Assert.Equal(8, triage.Record.Version);
        Assert.Equal(TriageState.Cancelled, triage.Record.State);

        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            8,
            "reopen",
            "Further review required");
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.Open, triage.Record.State);
        Assert.Equal(9, triage.Record.Version);
        Assert.Collection(
            triage.History,
            item => Assert.Equal("triage_created", item.EventType),
            item => Assert.Equal("triage_assigned", item.EventType),
            item => Assert.Equal("triage_finding_recorded", item.EventType),
            item => Assert.Equal("triage_response_linked", item.EventType),
            item => Assert.Equal("triage_state_completed", item.EventType),
            item => Assert.Equal("triage_finding_superseded", item.EventType),
            item => Assert.Equal("triage_state_cancelled", item.EventType),
            item => Assert.Equal("triage_case_linked", item.EventType),
            item => Assert.Equal("triage_case_unlinked", item.EventType),
            item => Assert.Equal("triage_state_open", item.EventType));
        Assert.All(
            triage.History.Skip(1),
            item =>
            {
                Assert.Equal(actor, item.Actor);
                Assert.Equal(nameof(Pegasus.Core.Identity.ActorKind.Staff), item.ActorKind);
            });
        Assert.Equal(
            nameof(Pegasus.Core.Identity.ActorKind.SystemWorker),
            triage.History[0].ActorKind);

        using var finalResponse = await client.GetAsync($"/Triage/{triageId:D}");
        var finalHtml = await finalResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, finalResponse.StatusCode);
        // The panel is named "Notes"; its entries are still the one permanent,
        // attributed history, and nothing offers to assign to whoever is
        // signed in.
        Assert.Contains(">Notes</h2>", finalHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Assign to me", finalHtml, StringComparison.Ordinal);
        Assert.Contains("Case unlinked", finalHtml, StringComparison.Ordinal);

    }

    private static async Task<TriageDetail> GetOnlyTriageAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        var summary = Assert.Single(await queries.ListAsync(null, CancellationToken.None));
        return Assert.IsType<TriageDetail>(
            await queries.GetAsync(summary.Id, CancellationToken.None));
    }

    private static async Task<TriageDetail> GetTriageAsync(
        IServiceProvider services,
        Guid triageId)
    {
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        return Assert.IsType<TriageDetail>(
            await queries.GetAsync(triageId, CancellationToken.None));
    }

    private static async Task<string> PostActionAsync(
        HttpClient client,
        Guid triageId,
        string antiforgeryToken,
        long expectedVersion,
        string actionName,
        string reason,
        params KeyValuePair<string, string>[] additionalFields)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create("__RequestVerificationToken", antiforgeryToken),
            KeyValuePair.Create("expectedVersion", expectedVersion.ToString(CultureInfo.InvariantCulture)),
            KeyValuePair.Create("operationKey", Guid.NewGuid().ToString("N")),
            KeyValuePair.Create("actionName", actionName),
            KeyValuePair.Create("reason", reason)
        };
        fields.AddRange(additionalFields);

        using var response = await client.PostAsync(
            $"/Triage/{triageId:D}?handler=Action",
            new FormUrlEncodedContent(fields));
        if (actionName is "link_case" or "unlink_case")
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(
                $"/Triage/{triageId:D}",
                response.Headers.Location?.OriginalString);
            using var redirected = await client.GetAsync(response.Headers.Location!);
            var redirectedHtml = await redirected.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, redirected.StatusCode);
            return redirectedHtml;
        }

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return html;
    }


    private static async Task<Guid> SeedCaseAsync(IServiceProvider services, Guid receiptId)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Triage test provider"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {"TRIAGE"}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {"TRIAGE31001"}, {"inspection"}, {"not_ready"}, {"pending"}, {receiptId}, {true}, {true}, {true}, {true}, {now}, {SeededCaseEntityVersion}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.Review)}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {receiptId}, {"manual_upload"}, {"triage-case-link"}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"triage-test-reader"}, {"1"}, {"triage-fixture"}, {1}, {"triage-case-link"}, {1}, {true}, {now})");
        return caseId;
    }


    private static async Task<IReadOnlyList<EvaluationRevision>> GetEvaluationRevisionsAsync(
        LocalDbTestDatabase database,
        Guid receiptId)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Revision
            FROM IntakeEvaluations
            WHERE ProcessedReceiptId = @receiptId
            ORDER BY Revision
            """;
        command.Parameters.AddWithValue("@receiptId", receiptId);

        var evaluations = new List<EvaluationRevision>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            evaluations.Add(new(
                reader.GetGuid(0),
                reader.GetInt32(1)));
        }

        return evaluations;
    }


    private sealed class AcceptedTriageMatchPolicy(int matchCount = 1) : IInstructionExtractionPolicy
    {
        private readonly QdosInstructionExtractionPolicy inner = new();

        public string PrincipalCode => inner.PrincipalCode;

        public InstructionExtractionResult Extract(
            IntakeSourceReadResult readResult,
            DateTimeOffset processedAtUtc,
            EstablishedPrincipalContext principalContext)
        {
            var result = inner.Extract(readResult, processedAtUtc, principalContext);
            if (result.Applicability != InstructionPolicyApplicability.Applicable)
            {
                return result;
            }

            var acceptedMatches = Enumerable.Range(1, matchCount)
                .Select(index => new IntakeEvidence(
                    IntakeEvidenceSource.EmailBody,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.AcceptedTriageMatch,
                    $"accepted-triage-request-{index}",
                    "The test fixture represents an independently accepted Triage matcher result.",
                    AcceptedMatcherKey,
                    1))
                .ToArray();
            return result with
            {
                Evidence = [.. result.Evidence, .. acceptedMatches]
            };
        }
    }

    private sealed record EvaluationRevision(Guid Id, int Revision);
}
