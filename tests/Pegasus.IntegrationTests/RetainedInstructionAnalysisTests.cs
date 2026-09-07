using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// AnalyzeRetainedInstruction against the real web host, the real reader and
/// the real migration, on retained material no route identified.
///
/// A QDOS-shaped letter arriving from a sender the mail route does not accept
/// is exactly that material: it parks at needs_sorting with no principal, and
/// until now nothing could read it. The analysis reads it from the document and
/// records what it says — creating no Case and touching no receipt.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RetainedInstructionAnalysisTests
{
    private const string QdosShapedBody =
        "QDOS\r\n"
        + "Our Client: Fixture Claimant\r\n"
        + "Our Client’s Vehicle: Ford Focus\r\n"
        + "Registration: AB12 CDE\r\n"
        + "Claimant Name: Fixture Claimant\r\n"
        + "Claim Number: 12345/1\r\n";

    private const string UnrecognisedBody =
        "Please find attached our client's paperwork. We will call to arrange.\r\n";

    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    [Fact]
    public async Task ARetainedQdosLetterIsAnalysedFromTheDocumentAndAllocatesNothing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var host = WithAnalysis(factory);
        var receiptId = await RetainAsync(factory, host, QdosShapedBody, "retained-qdos.eml");

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);

        // Precondition: no route identified it, so it is retained material.
        Assert.Equal(IntakeDecision.NeedsSorting, receipt!.Decision);
        Assert.Null(receipt.InstructionDraft?.SuggestedPrincipalCode);

        var result = await services.GetRequiredService<AnalyzeRetainedInstruction>().ExecuteAsync(
            new(StaffActor(), receiptId, receipt.Version, $"analysis:{receiptId:N}:1"));

        Assert.Equal(RetainedInstructionAnalysisOutcome.Analyzed, result.Outcome);
        var analysis = result.Analysis!;
        Assert.NotEmpty(analysis.Candidates);

        // The document proposed the principal - as a candidate, not a decision.
        var principal = Assert.Single(
            analysis.Candidates,
            candidate => candidate.Field == AnalyzeRetainedInstruction.SuggestedPrincipalField);
        Assert.Equal(QdosInstructionExtractionPolicy.SupportedPrincipalCode, principal.RawValue);
        Assert.Equal(
            QdosInstructionExtractionPolicy.DocumentProfileKeyValue,
            principal.PolicyKey);

        // What the letter actually says reached the record.
        Assert.Contains(
            analysis.Candidates,
            candidate => candidate.Field == "Vehicle registration"
                && candidate.RawValue is not null
                && candidate.RawValue.Contains("AB12", StringComparison.OrdinalIgnoreCase));

        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();

        // The rows are real, and the candidates are keyed on the retained asset
        // with null document ids: a pre-case receipt has no Case document.
        var row = await context.Set<RetainedInstructionAnalysisEntity>().AsNoTracking()
            .SingleAsync(item => item.IntakeReceiptId == receiptId);
        Assert.Equal(nameof(RetainedInstructionAnalysisOutcome.Analyzed), row.State);
        Assert.Equal(receipt.Version, row.ExpectedReceiptVersion);
        var persisted = await context.Set<IntakeSourceCandidateEntity>().AsNoTracking()
            .Where(item => item.AnalysisId == row.Id)
            .ToArrayAsync();
        Assert.Equal(analysis.Candidates.Count, persisted.Length);
        Assert.All(persisted, candidate =>
        {
            Assert.Null(candidate.DocumentVersionId);
            Assert.Equal(row.IntakeAssetId, candidate.IntakeAssetId);
            Assert.Equal(row.SourceSha256, candidate.SourceSha256);
        });

        // It allocates nothing.
        Assert.Equal(0, await context.Cases.CountAsync());
        Assert.Equal(0, await context.CaseIntakeLinks.CountAsync());

        // And the receipt itself is untouched - asserted fact by fact, because a
        // whole-record comparison also pins every unrelated field the pipeline
        // happens to record (the mail route's own no-match reason among them)
        // and fails for reasons that have nothing to do with this command.
        var after = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(after);
        Assert.Equal(receipt.Version, after!.Version);
        Assert.Equal(IntakeDecision.NeedsSorting, after.Decision);
        Assert.Equal(receipt.DecisionReason, after.DecisionReason);
        Assert.Null(after.InstructionDraft);
        Assert.Null(after.AcceptedCaseId);
        Assert.Null(after.ManualLinkedCaseId);
        Assert.Null(after.AllocationState);
        Assert.Equal(receipt.Fields.Count, after.Fields.Count);

        // The shared candidate query sees the same rows, scoped to the asset.
        var queried = await services.GetRequiredService<ISourceCandidateQueries>().GetAsync(
            StaffActor(), receiptId, null, row.IntakeAssetId, CancellationToken.None);
        Assert.Equal(analysis.Candidates.Count, queried.Count);
        Assert.All(queried, candidate =>
        {
            Assert.Null(candidate.DocumentId);
            Assert.Null(candidate.DocumentVersionId);
            Assert.Equal(row.IntakeAssetId, candidate.IntakeAssetId);
        });

        var reports = services.GetRequiredService<IThirdPartyReportCandidateQueries>();
        Assert.Empty(await reports.GetAsync(
            StaffActor(), receiptId, null, row.IntakeAssetId, CancellationToken.None));
        Assert.Empty(await reports.GetAsync(
            StaffActor(), Guid.NewGuid(), null, null, CancellationToken.None));
        Assert.Empty(await reports.GetAsync(
            StaffActor(), receiptId, Guid.NewGuid(), null, CancellationToken.None));
        Assert.Empty(await reports.GetAsync(
            StaffActor(), receiptId, null, Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => reports.GetAsync(
            ActionActor.RequestLink(Guid.NewGuid()),
            receiptId,
            null,
            row.IntakeAssetId,
            CancellationToken.None));
    }

    [Fact]
    public async Task AReplayUnderTheSameKeyWritesNoDuplicateCandidates()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var host = WithAnalysis(factory);
        var receiptId = await RetainAsync(factory, host, QdosShapedBody, "retained-replay.eml");

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var command = services.GetRequiredService<AnalyzeRetainedInstruction>();
        var key = $"analysis:{receiptId:N}:1";

        var first = await command.ExecuteAsync(
            new(StaffActor(), receiptId, receipt!.Version, key));
        var second = await command.ExecuteAsync(
            new(StaffActor(), receiptId, receipt.Version, key));

        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);
        Assert.Equal(first.Analysis!.Id, second.Analysis!.Id);

        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var row = await context.Set<RetainedInstructionAnalysisEntity>().AsNoTracking()
            .SingleAsync(item => item.IntakeReceiptId == receiptId);
        Assert.Equal(
            first.Analysis.Candidates.Count,
            await context.Set<IntakeSourceCandidateEntity>().AsNoTracking()
                .CountAsync(item => item.AnalysisId == row.Id));
    }

    [Fact]
    public async Task ADocumentNoProfileRecognisesRecordsNoProfileWithNoCandidates()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var host = WithAnalysis(factory);
        var receiptId = await RetainAsync(factory, host, UnrecognisedBody, "retained-other.eml");

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);

        var result = await services.GetRequiredService<AnalyzeRetainedInstruction>().ExecuteAsync(
            new(StaffActor(), receiptId, receipt!.Version, $"analysis:{receiptId:N}:1"));

        Assert.Equal(RetainedInstructionAnalysisOutcome.NoProfile, result.Outcome);
        Assert.Empty(result.Analysis!.Candidates);

        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        // The row exists so the receipt can say the question was asked and
        // answered; a re-evaluation under a new key is distinguishable from
        // never having run at all.
        Assert.Equal(
            nameof(RetainedInstructionAnalysisOutcome.NoProfile),
            (await context.Set<RetainedInstructionAnalysisEntity>().AsNoTracking()
                .SingleAsync(item => item.IntakeReceiptId == receiptId)).State);
        Assert.Equal(0, await context.Cases.CountAsync());
    }

    [Fact]
    public async Task AStaleReceiptVersionIsRefusedAndRecordsNothing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var host = WithAnalysis(factory);
        var receiptId = await RetainAsync(factory, host, QdosShapedBody, "retained-stale.eml");

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);

        var result = await services.GetRequiredService<AnalyzeRetainedInstruction>().ExecuteAsync(
            new(StaffActor(), receiptId, receipt!.Version + 1, $"analysis:{receiptId:N}:stale"));

        Assert.Equal(RetainedInstructionAnalysisOutcome.Conflict, result.Outcome);
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.Equal(
            0,
            await context.Set<RetainedInstructionAnalysisEntity>().CountAsync());
    }

    /// <summary>
    /// Retains one e-mail as an intake receipt no route identifies. The sender
    /// is deliberately outside every accepted direct domain, so the mail route
    /// does not accept it, no principal is established, and the material is
    /// exactly the unresolved retained material this command exists for.
    /// </summary>
    private static async Task<Guid> RetainAsync(
        IntakeWebApplicationFactory factory,
        WebApplicationFactory<Program> host,
        string body,
        string fileName)
    {
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        var email = IntakeTestEvidence.CreateEmail(
            fileName,
            body,
            senderAddress: "post@an-unrecognised-broker.example",
            subject: "Instruction paperwork");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content,
            Guid.NewGuid().ToString("N"));
        return IntakeWebDriver.ReceiptId(upload);
    }

    /// <summary>
    /// Composes the analysis surface for these tests. The production
    /// registrations are Stream A's to add to <c>DependencyInjection.cs</c>;
    /// until they land the command resolves only here, which is stated in the
    /// C01 report rather than hidden behind an optional dependency that quietly
    /// does nothing.
    /// </summary>
    private static WebApplicationFactory<Program> WithAnalysis(
        IntakeWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<InstructionExtractionPolicySelector>();
            services.AddScoped<IReadLogicalDocumentVersion, RetainedIntakeAssetReader>();
            services.AddScoped<EfRetainedInstructionAnalysisStore>();
            services.AddScoped<IRetainedInstructionAnalysisStore>(provider =>
                provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
            services.AddScoped<ISourceCandidateQueries>(provider =>
                provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
            services.AddScoped<IThirdPartyReportCandidateQueries>(provider =>
                provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
            services.AddScoped<IGetLatestRetainedInstructionAnalysis,
                GetLatestRetainedInstructionAnalysis>();
            services.AddScoped<AnalyzeRetainedInstruction>();
        }));

    /// <summary>
    /// A C-owned stand-in for A04's logical-document reader, covering only what
    /// a pre-case retained asset needs: resolve the asset by identity within its
    /// receipt, read the bytes through <see cref="IIntakeArtifactStore"/> by the
    /// recorded storage key, and verify them against the caller's expected hash
    /// and length before handing any back.
    ///
    /// The verification is the point. A double that returned bytes unchecked
    /// would let the command pass a test the real reader would fail, and the
    /// command's whole claim to read "the immutable source" rests on it.
    /// </summary>
    private sealed class RetainedIntakeAssetReader(
        IIntakeReceiptQueries receiptQueries,
        IIntakeArtifactStore artifactStore) : IReadLogicalDocumentVersion
    {
        public async Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
            if (request.IntakeAssetId is not { } assetId
                || request.IntakeReceiptId is not { } receiptId)
            {
                throw new NotSupportedException(
                    "This reader serves retained intake assets only.");
            }

            var receipt = await receiptQueries.GetAsync(receiptId, cancellationToken)
                ?? throw new KeyNotFoundException("The intake receipt does not exist.");
            var asset = receipt.AssetRecords.SingleOrDefault(record => record.Id == assetId)
                ?? throw new KeyNotFoundException("The retained asset does not exist.");

            var content = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken)
                ?? throw new IntakeArtifactIntegrityException();
            var hash = Convert.ToHexString(SHA256.HashData(content.Span));
            if (content.Length != request.ExpectedContentLength
                || !string.Equals(hash, request.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new IntakeArtifactIntegrityException();
            }

            return new(
                new MemoryStream(content.ToArray(), writable: false),
                null,
                null,
                assetId,
                hash,
                content.Length,
                asset.FileName,
                asset.MediaType);
        }
    }
}
