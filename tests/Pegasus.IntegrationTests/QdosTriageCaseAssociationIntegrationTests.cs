using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

public sealed partial class QdosTriageIntegrationTests
{
    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task CaseAssociationUsesCanonicalWorkflowVersionAndActiveCaseLease()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: new AcceptedTriageMatchPolicy());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "triage-case-association.eml",
            "QDOS instruction\r\nClaimant Name: Association Claimant\r\nClaim Number: TRIAGE-ASSOCIATION\r\nVehicle Registration: CD34 EFG");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(factory, client, email.FileName,
        email.MediaType,
        email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        var initial = await GetOnlyTriageAsync(factory.Services);
        var triageId = initial.Record.Id;
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var caseId = await SeedCaseAsync(factory.Services, receiptId);
        var firstCaseLease = await ClaimCaseLeaseAsync(
            factory.Services,
            caseId,
            0,
            actor,
            "claim-first-case-association-lease");
        var link = services.GetRequiredService<ILinkTriageCase>();
        var unlink = services.GetRequiredService<IUnlinkTriageCase>();

        var hiddenVersionRequest = new TriageCaseLinkRequest(
            triageId,
            caseId,
            0,
            SeededCaseEntityVersion,
            actor,
            "link-case-with-hidden-version",
            "The hidden case-row version must not authorize association",
            firstCaseLease.Token);
        var hiddenVersionConflict = await Assert.ThrowsAsync<CaseVersionConflictException>(
            () => link.ExecuteAsync(hiddenVersionRequest, CancellationToken.None));
        Assert.Equal(SeededCaseEntityVersion, hiddenVersionConflict.ExpectedVersion);
        Assert.Equal(0, hiddenVersionConflict.ActualVersion);

        var linkRequest = hiddenVersionRequest with
        {
            ExpectedCaseVersion = 0,
            OperationKey = "link-case-canonical-version",
            Reason = "Associate retained Triage evidence with the case"
        };
        await link.ExecuteAsync(linkRequest, CancellationToken.None);
        await link.ExecuteAsync(linkRequest, CancellationToken.None);
        await Assert.ThrowsAsync<TriageOperationConflictException>(
            () => link.ExecuteAsync(
                linkRequest with { Reason = "Altered association request" },
                CancellationToken.None));

        var linked = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(caseId, linked.Record.LinkedCaseId);
        Assert.Equal(1, linked.Record.Version);
        var afterLink = await ReadCaseAssociationPersistenceAsync(factory.Database, caseId);
        Assert.Equal(SeededCaseEntityVersion, afterLink.CaseEntityVersion);
        Assert.Equal(1, afterLink.WorkflowVersion);
        Assert.False(afterLink.HasActiveLease);
        Assert.Collection(
            afterLink.Events,
            item =>
            {
                Assert.Equal("triage_case_linked", item.EventType);
                Assert.Equal(linkRequest.OperationKey, item.OperationKey);
                Assert.Equal(0, item.BeforeVersion);
                Assert.Equal(1, item.AfterVersion);
            });

        var consumedLeaseRequest = new TriageCaseLinkRequest(
            triageId,
            caseId,
            1,
            1,
            actor,
            "unlink-case-with-consumed-lease",
            "A consumed case lease must not authorize another mutation",
            firstCaseLease.Token);
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(
            () => unlink.ExecuteAsync(consumedLeaseRequest, CancellationToken.None));

        var secondCaseLease = await ClaimCaseLeaseAsync(
            factory.Services,
            caseId,
            1,
            actor,
            "claim-second-case-association-lease");
        var staleCaseVersionRequest = consumedLeaseRequest with
        {
            ExpectedCaseVersion = 0,
            OperationKey = "unlink-case-with-stale-version",
            Reason = "A stale canonical workflow version must fail",
            CaseEditLeaseToken = secondCaseLease.Token
        };
        var staleCaseVersion = await Assert.ThrowsAsync<CaseVersionConflictException>(
            () => unlink.ExecuteAsync(staleCaseVersionRequest, CancellationToken.None));
        Assert.Equal(0, staleCaseVersion.ExpectedVersion);
        Assert.Equal(1, staleCaseVersion.ActualVersion);

        var wrongCaseTokenRequest = staleCaseVersionRequest with
        {
            ExpectedCaseVersion = 1,
            OperationKey = "unlink-case-with-wrong-token",
            Reason = "A different case lease token must fail",
            CaseEditLeaseToken = new string('e', 64)
        };
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(
            () => unlink.ExecuteAsync(wrongCaseTokenRequest, CancellationToken.None));

        var unlinkRequest = wrongCaseTokenRequest with
        {
            OperationKey = "unlink-case-canonical-version",
            Reason = "Correct the retained Triage-to-case association",
            CaseEditLeaseToken = secondCaseLease.Token
        };
        await unlink.ExecuteAsync(unlinkRequest, CancellationToken.None);
        await unlink.ExecuteAsync(unlinkRequest, CancellationToken.None);
        await Assert.ThrowsAsync<TriageOperationConflictException>(
            () => unlink.ExecuteAsync(
                unlinkRequest with { Reason = "Altered disassociation request" },
                CancellationToken.None));

        var final = await GetTriageAsync(factory.Services, triageId);
        Assert.Null(final.Record.LinkedCaseId);
        Assert.Equal(2, final.Record.Version);
        Assert.Collection(
            final.History,
            item => Assert.Equal("triage_created", item.EventType),
            item => Assert.Equal("triage_case_linked", item.EventType),
            item => Assert.Equal("triage_case_unlinked", item.EventType));

        var persistence = await ReadCaseAssociationPersistenceAsync(factory.Database, caseId);
        Assert.Equal(SeededCaseEntityVersion, persistence.CaseEntityVersion);
        Assert.Equal(2, persistence.WorkflowVersion);
        Assert.False(persistence.HasActiveLease);
        Assert.Collection(
            persistence.Events,
            item =>
            {
                Assert.Equal("triage_case_linked", item.EventType);
                Assert.Equal(linkRequest.OperationKey, item.OperationKey);
                Assert.Equal(0, item.BeforeVersion);
                Assert.Equal(1, item.AfterVersion);
            },
            item =>
            {
                Assert.Equal("triage_case_unlinked", item.EventType);
                Assert.Equal(unlinkRequest.OperationKey, item.OperationKey);
                Assert.Equal(1, item.BeforeVersion);
                Assert.Equal(2, item.AfterVersion);
            });
    }

    [Fact]
    [Trait("Category", "QdosAlphaAcceptance")]
    public async Task IncomingFormalInstructionSharingVrmAndPrincipalWithOpenTriageDoesNotAutoLinkOrCloseTriage()
    {
        const string sharedVrm = "CD34 EFG";
        const string normalizedVrm = "CD34EFG";

        var extractionPolicy = new ConditionalTriageMatchPolicy(readResult =>
            string.Equals(
                readResult.InstructionDraft?.ClaimNumber,
                "TRIAGE-REQUEST",
                StringComparison.OrdinalIgnoreCase));

        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            extractionPolicy: extractionPolicy);
        using var client = IntakeWebDriver.CreateClient(factory);

        var triageEmail = IntakeTestEvidence.CreateEmail(
            "triage-request.eml",
            $"QDOS instruction\r\nClaimant Name: Triage Claimant\r\nClaim Number: TRIAGE-REQUEST\r\nVehicle Registration: {sharedVrm}");
        var triageUpload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            triageEmail.FileName,
            triageEmail.MediaType,
            triageEmail.Content);
        var triageReceiptId = IntakeWebDriver.ReceiptId(triageUpload);

        var initialTriage = await GetOnlyTriageAsync(factory.Services);
        var triageId = initialTriage.Record.Id;
        var initialVersion = initialTriage.Record.Version;

        Assert.Equal(TriageState.Open, initialTriage.Record.State);
        Assert.Null(initialTriage.Record.LinkedCaseId);
        Assert.Equal(normalizedVrm, initialTriage.Record.NormalizedVehicleRegistration);

        var formalInstructionEmail = IntakeTestEvidence.CreateEmail(
            "formal-instruction.eml",
            $"QDOS instruction\r\nClaimant Name: Formal Claimant\r\nClaim Number: FORMAL-001\r\nVehicle Registration: {sharedVrm}");
        var instructionUpload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            formalInstructionEmail.FileName,
            formalInstructionEmail.MediaType,
            formalInstructionEmail.Content);
        var instructionReceiptId = IntakeWebDriver.ReceiptId(instructionUpload);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
            var instructionReceipt = Assert.IsType<IntakeReceipt>(
                await receipts.GetAsync(instructionReceiptId, CancellationToken.None));

            Assert.Equal(IntakeDecision.CaseCreated, instructionReceipt.Decision);
            Assert.NotNull(instructionReceipt.CurrentCaseId);
            Assert.Equal(normalizedVrm, instructionReceipt.InstructionDraft?.VehicleRegistration);
        }

        var remainingTriage = await GetTriageAsync(factory.Services, triageId);
        Assert.Equal(TriageState.Open, remainingTriage.Record.State);
        Assert.Null(remainingTriage.Record.LinkedCaseId);
        Assert.Equal(initialVersion, remainingTriage.Record.Version);
        Assert.DoesNotContain(
            remainingTriage.History,
            entry => entry.EventType is "triage_case_linked" or "triage_state_changed");
    }

    private sealed class ConditionalTriageMatchPolicy(Func<IntakeSourceReadResult, bool> isTriage) : IInstructionExtractionPolicy
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

            if (isTriage(readResult))
            {
                var acceptedMatches = new[]
                {
                    new IntakeEvidence(
                        IntakeEvidenceSource.SystemDefault,
                        IntakeEvidenceStrength.Strong,
                        IntakeEvidenceFinding.AcceptedTriageMatch,
                        "accepted-triage-request-1",
                        "The test fixture represents an independently accepted Triage matcher result.",
                        AcceptedMatcherKey,
                        1)
                };
                return result with
                {
                    Evidence = [.. result.Evidence, .. acceptedMatches]
                };
            }

            return result;
        }
    }

    private static async Task<CaseEditLease> ClaimCaseLeaseAsync(
        IServiceProvider services,
        Guid caseId,
        long expectedVersion,
        ActionActor actor,
        string operationKey)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new ClaimCaseEditLeaseRequest(
                caseId,
                expectedVersion,
                actor,
                operationKey),
            CancellationToken.None);
    }

    private static async Task<CaseAssociationPersistence> ReadCaseAssociationPersistenceAsync(
        LocalDbTestDatabase database,
        Guid caseId)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        long caseEntityVersion;
        long workflowVersion;
        bool hasActiveLease;
        await using (var versionCommand = connection.CreateCommand())
        {
            versionCommand.CommandText = """
                SELECT c.Version,
                       w.Version,
                       CASE WHEN w.EditLeaseTokenHash IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END
                FROM Cases AS c
                INNER JOIN CaseWorkflows AS w ON w.CaseId = c.Id
                WHERE c.Id = @caseId
                """;
            versionCommand.Parameters.AddWithValue("@caseId", caseId);
            await using var reader = await versionCommand.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            caseEntityVersion = reader.GetInt64(0);
            workflowVersion = reader.GetInt64(1);
            hasActiveLease = reader.GetBoolean(2);
            Assert.False(await reader.ReadAsync());
        }

        var events = new List<CaseAssociationEvent>();
        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.CommandText = """
                SELECT EventType, OperationKey, BeforeVersion, AfterVersion
                FROM CaseWorkflowEvents
                WHERE CaseId = @caseId
                ORDER BY AfterVersion
                """;
            eventCommand.Parameters.AddWithValue("@caseId", caseId);
            await using var reader = await eventCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                events.Add(new(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3)));
            }
        }

        return new(caseEntityVersion, workflowVersion, hasActiveLease, events);
    }

    private sealed record CaseAssociationPersistence(
        long CaseEntityVersion,
        long WorkflowVersion,
        bool HasActiveLease,
        IReadOnlyList<CaseAssociationEvent> Events);

    private sealed record CaseAssociationEvent(
        string EventType,
        string OperationKey,
        long BeforeVersion,
        long AfterVersion);
}
