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
    private const long SeededCaseEntityVersion = 37;

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task AcceptedTriageMatchEvidenceCreatesOneReplaySafeTriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-request.eml");
        const string replayToken = "77777777777777777777777777777777";

        var first = await MailboxIntakeTestData.SubmitAndProcessAsync(
            factory.Services, email, replayToken);
        var replay = await MailboxIntakeTestData.SubmitAndProcessAsync(
            factory.Services, email, replayToken);
        var receiptId = first;

        Assert.Equal(receiptId, replay);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.IsType<CreateTriageFromIntake>(
            scope.ServiceProvider.GetRequiredService<ICreateTriageFromIntake>());
        var triageQueries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        var summary = Assert.Single(
            await triageQueries.ListAsync(null, CancellationToken.None));
        var detail = Assert.IsType<TriageDetail>(
            await triageQueries.GetAsync(summary.CaseId, CancellationToken.None));
        var evaluation = Assert.Single(
            await GetEvaluationRevisionsAsync(factory.Database, receiptId));

        Assert.Equal(1, evaluation.Revision);
        Assert.Equal(receiptId, detail.Record.Origin?.ReceiptId);
        Assert.Equal(evaluation.Id, detail.Record.Origin?.EvaluationRevisionId);
        Assert.Equal("VO75DFJ", detail.Record.NormalizedVehicleRegistration);
        Assert.Equal(TriageState.Open, detail.Record.State);
        Assert.Null(detail.Record.LinkedInstructionCaseId);
        // The Triage Case takes the first QDOS Case/PO of the factory's year.
        Assert.Equal("t.QDOS31001", detail.Record.Reference);
        Assert.Empty(detail.Findings);
        Assert.Empty(detail.ResponseEvidence);
        var created = Assert.Single(detail.History);
        Assert.Equal("triage_created", created.EventType);
        Assert.Contains(PrincipalMailClassificationPolicy.Key, created.Reason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task CaseCreatedWithoutAcceptedTriageMatchEvidenceDoesNotCreateTriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "ordinary-instruction.eml",
            "Please see the attached instruction.",
            attachments:
            [
                ("instruction.pdf", "application/pdf",
                    IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
                        claimantName: "Ordinary Claimant", claimNumber: "ORDINARY-001", registration: "AB12 CDE"))
            ]);

        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);

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
            "Please see the attached instruction.",
            attachments:
            [
                ("instruction.pdf", "application/pdf",
                    IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
                        claimantName: "No Registration", claimNumber: "TRIAGE-002", registration: ""))
            ]);

        var sortingReceiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(
            factory.Services, "needs-sorting.eml", "message/rfc822", needsSorting);
        var missingRegistrationReceiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(
            factory.Services, missingRegistration);
        var blockedReceiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(
            factory.Services, "unsupported.txt", "text/plain", Encoding.UTF8.GetBytes("Unsupported intake source."));

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

    /// <summary>
    /// The Triage Case page has no Edit step: each action posts once and its
    /// save claims and releases the Triage edit scope itself. Completion needs
    /// a recorded finding and nothing else: no reason and no sent reply.
    /// </summary>
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task AuthenticatedTriagePageExecutesLifecycleWithVersionsAndPermanentHistory()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-lifecycle.eml");
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        var triage = await GetOnlyTriageAsync(factory.Services);
        var triageId = triage.Record.CaseId;
        var actor = DevelopmentOfflineIdentity.AdministratorId.ToString("D");

        using var detailResponse = await client.GetAsync($"/Cases/{triageId:D}");
        var detailHtml = await detailResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Contains("class=\"record triage-record\"", detailHtml, StringComparison.Ordinal);
        // A Triage Case has the Files every Case has, before its Notes.
        var filesAt = detailHtml.IndexOf("id=\"section-files\"", StringComparison.Ordinal);
        Assert.True(filesAt >= 0, "The Triage Case renders no Files panel.");
        Assert.True(
            filesAt < detailHtml.IndexOf(">Notes</h2>", StringComparison.Ordinal),
            "The Files panel must come before Notes.");
        Assert.DoesNotContain("name=\"caseEditLeaseToken\"", detailHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", detailHtml, StringComparison.Ordinal);

        // The record's own identifiers are internal and never printed.
        Assert.DoesNotContain("Source SHA-256", detailHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Evaluation revision", detailHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(triage.Record.Origin!.SourceHash, detailHtml, StringComparison.Ordinal);

        // Complete is offered only once a finding is recorded.
        Assert.DoesNotContain("data-triage-complete", detailHtml, StringComparison.Ordinal);
        Assert.Contains("data-dialog=\"triage-assign-dialog\"", detailHtml, StringComparison.Ordinal);
        Assert.Contains("id=\"triage-assignee\"", detailHtml, StringComparison.Ordinal);

        // This request was submitted directly, with no retained message, so
        // there is no message to open and no receipt page instead.
        Assert.DoesNotContain("data-triage-action=\"open-message\"", detailHtml, StringComparison.Ordinal);
        var antiforgeryToken = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        var assignedHtml = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            0,
            "assign",
            reason: null,
            KeyValuePair.Create("assigneeId", DevelopmentOfflineIdentity.AdministratorId.ToString("D")));
        Assert.Contains("Assigned to ", assignedHtml, StringComparison.Ordinal);
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
        Assert.Contains("changed while you were working", staleHtml, StringComparison.Ordinal);
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(1, triage.Record.Version);
        Assert.Equal(2, triage.History.Count);

        var recordedHtml = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            1,
            "record_finding",
            "Reviewed assessment",
            KeyValuePair.Create("roadworthiness", nameof(RoadworthinessFinding.Unroadworthy)),
            KeyValuePair.Create("assessment", nameof(AssessmentFinding.TotalLoss)));
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.FindingRecorded, recordedHtml, StringComparison.Ordinal);
        Assert.Contains("data-triage-complete", recordedHtml, StringComparison.Ordinal);
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.FindingRecorded, triage.Record.State);
        Assert.Equal(2, triage.Record.Version);
        var initialFinding = Assert.Single(triage.Findings);

        // One post, no reason and no response evidence.
        Assert.Empty(triage.ResponseEvidence);
        var completedHtml = await PostActionAsync(client, triageId, antiforgeryToken, 2, "complete", reason: null);
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.Completed, completedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Post-send correction", completedHtml, StringComparison.Ordinal);
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.RecordCorrection, completedHtml, StringComparison.Ordinal);
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.Completed, triage.Record.State);
        Assert.Equal(3, triage.Record.Version);
        Assert.Equal(CompleteTriage.Reason, triage.History[^1].Reason);

        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            3,
            "supersede_finding",
            "Correction after further retained evidence",
            KeyValuePair.Create("roadworthiness", nameof(RoadworthinessFinding.Roadworthy)),
            KeyValuePair.Create("assessment", nameof(AssessmentFinding.Repairable)),
            KeyValuePair.Create("supersedesFindingId", initialFinding.Id.ToString("D")));
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.FindingRecorded, triage.Record.State);
        Assert.Equal(4, triage.Record.Version);
        Assert.Equal(initialFinding.Id, triage.Findings.Single(
            finding => finding.SupersedesFindingId is not null).SupersedesFindingId);

        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            4,
            "cancel",
            "Provider withdrew the request");
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.Cancelled, triage.Record.State);
        Assert.Equal(5, triage.Record.Version);

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
            5,
            "link_case",
            "Must not bypass another holder",
            KeyValuePair.Create("caseId", caseId.ToString("D")));
        // The holder is disclosed by staff account, never by identifier, and the wording and
        // clock are the ones the case workspace uses.
        Assert.Contains("Case locked - ", unavailableCaseHtml, StringComparison.Ordinal);
        Assert.Contains("is editing the case", unavailableCaseHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Editing becomes available", unavailableCaseHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(otherHolder.SubjectId, unavailableCaseHtml, StringComparison.OrdinalIgnoreCase);
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Null(triage.Record.LinkedInstructionCaseId);
        Assert.Equal(5, triage.Record.Version);
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

        var linkedHtml = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            5,
            "link_case",
            "Associated later instruction",
            KeyValuePair.Create("caseId", caseId.ToString("D")));
        Assert.Contains(Pegasus.Web.Presentation.OperatorLabels.Triage.CaseLinked, linkedHtml, StringComparison.Ordinal);
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(caseId, triage.Record.LinkedInstructionCaseId);
        Assert.Equal(6, triage.Record.Version);
        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            6,
            "unlink_case",
            "Association corrected",
            KeyValuePair.Create("caseId", caseId.ToString("D")));
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Null(triage.Record.LinkedInstructionCaseId);
        Assert.Equal(7, triage.Record.Version);
        Assert.Equal(TriageState.Cancelled, triage.Record.State);

        _ = await PostActionAsync(
            client,
            triageId,
            antiforgeryToken,
            7,
            "reopen",
            "Further review required");
        triage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.Open, triage.Record.State);
        Assert.Equal(8, triage.Record.Version);
        Assert.Collection(
            triage.History,
            item => Assert.Equal("triage_created", item.EventType),
            item => Assert.Equal("triage_assigned", item.EventType),
            item => Assert.Equal("triage_finding_recorded", item.EventType),
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

        // Every save ended its own hold: nothing is left holding the record.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IEditScopeLeases>().GetActiveAsync(
                EditScopeKind.Triage,
                triageId,
                ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
                CancellationToken.None));
        }

        using var finalResponse = await client.GetAsync($"/Cases/{triageId:D}");
        var finalHtml = await finalResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, finalResponse.StatusCode);
        Assert.Contains(">Notes</h2>", finalHtml, StringComparison.Ordinal);
        Assert.Contains("Case unlinked", finalHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Triage Case's Principal is the Case's own, fixed when the Case is
    /// created from the receipt that established it (decision T): the record
    /// shows it and offers no Set principal correction.
    /// </summary>
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task ATriageCaseCarriesItsCasePrincipalAndOffersNoPrincipalCorrection()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-principal.eml");
        await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        var triage = await GetOnlyTriageAsync(factory.Services);
        var triageId = triage.Record.CaseId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var triageCase = await context.Cases.AsNoTracking().SingleAsync(item => item.Id == triageId);
            Assert.Equal(triageCase.PrincipalId, triage.Record.PrincipalId);
            Assert.Equal(triageCase.Reference, triage.Record.Reference);
            Assert.Equal(CaseTypeCodes.Triage, triageCase.Type);
            Assert.Null(triageCase.InitialState);
        }
        Assert.Equal("QDOS", triage.PrincipalCode);

        using var detailResponse = await client.GetAsync($"/Cases/{triageId:D}");
        var detailHtml = await detailResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.DoesNotContain("triage-principal-dialog", detailHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"triage-principal\"", detailHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Set principal", detailHtml, StringComparison.Ordinal);
    }

    private static async Task<TriageDetail> GetOnlyTriageAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITriageQueries>();
        var summary = Assert.Single(await queries.ListAsync(null, CancellationToken.None));
        return Assert.IsType<TriageDetail>(
            await queries.GetAsync(summary.CaseId, CancellationToken.None));
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

    /// <summary>
    /// One Triage action, posted once as the page posts it: no Edit step and
    /// no edit token. Link and Unlink case redirect back to the record.
    /// </summary>
    private static async Task<string> PostActionAsync(
        HttpClient client,
        Guid triageId,
        string antiforgeryToken,
        long expectedVersion,
        string actionName,
        string? reason,
        params KeyValuePair<string, string>[] additionalFields)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create("__RequestVerificationToken", antiforgeryToken),
            KeyValuePair.Create("expectedVersion", expectedVersion.ToString(CultureInfo.InvariantCulture)),
            KeyValuePair.Create("operationKey", Guid.NewGuid().ToString("N")),
            KeyValuePair.Create("actionName", actionName)
        };
        if (reason is not null)
        {
            fields.Add(KeyValuePair.Create("reason", reason));
        }
        fields.AddRange(additionalFields);

        using var response = await client.PostAsync(
            $"/Cases/{triageId:D}?handler=TriageAction",
            new FormUrlEncodedContent(fields));
        if (actionName is "link_case" or "unlink_case")
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(
                $"/Cases/{triageId:D}",
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
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {"TRIAGE31001"}, {"inspection"}, {"not_ready"}, {"pending"}, {receiptId}, {true}, {true}, {now}, {SeededCaseEntityVersion}, {Guid.NewGuid()})");
        await CaseWorkFixture.InsertPrimaryWorksAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.Review)}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (WorkId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {receiptId}, {"manual_upload"}, {"triage-case-link"}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"triage-test-reader"}, {"1"}, {"triage-fixture"}, {1}, {"triage-case-link"}, {1}, {true}, {now})");
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
    private sealed record EvaluationRevision(Guid Id, int Revision);

}
