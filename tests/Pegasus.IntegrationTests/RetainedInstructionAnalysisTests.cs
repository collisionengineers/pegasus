using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Core.Intake.Unidentified;
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
    /// Every one of the eighty-one genuine originals the reference pack carries
    /// reaches extraction through <see cref="IAnalyzeRetainedInstruction"/> —
    /// resolved from the host's own composition, on real SQL, over material
    /// retained by the ordinary manual-upload path — and not one of them
    /// allocates anything.
    ///
    /// This is the integration proof the direct-drive corpus suite cannot give.
    /// <see cref="Top15InstructionCorpusTests"/> hands the same eighty-one
    /// originals to the reader and the selector by hand: it proves the DOCUMENTS
    /// are identifiable. It never opens a retained source by identity, never
    /// persists a candidate, never replays an operation key and never counts a
    /// Case. So it cannot say whether the product actually reads its own
    /// retained material, and this says exactly that.
    ///
    /// The expectations are NOT copied. One list of expectations per concept:
    /// the pack-relative path, the recorded hash and the labeller's profile are
    /// read from <see cref="Top15InstructionCorpusTests.Expectations"/>, so a
    /// batch that adds a profile adds rows in one place and both suites see
    /// them.
    ///
    /// What it asserts, per original: the bytes hash to what the pack records;
    /// the outcome is <see cref="RetainedInstructionAnalysisOutcome.Analyzed"/>;
    /// the profile the document selected is the one the labeller assigned; the
    /// principal arrives as a review-only candidate and never as a decision;
    /// every persisted row carries the retained source's own hash and the
    /// occurrence the analysis recorded; a second execution under the same key
    /// and version replays without writing a second row; and the database holds
    /// no Case, no Case link and no manual association at any point.
    ///
    /// Ambiguity and missing counts are MEASURED per profile and written to
    /// <c>artifacts/evaluation/v1-intake/retained-analysis-corpus.md</c>, never
    /// asserted — the same discipline the direct-drive suite keeps, and for the
    /// same reason: no accuracy threshold may be claimed without
    /// operator-labelled holdouts. An original that returns Ambiguous or
    /// NoProfile is a FAILURE, not a measurement; nothing here relabels one as
    /// the other.
    ///
    /// An original the READER could not deliver is a third thing, and it is
    /// recorded as the direct-drive suite records it: INCONCLUSIVE, in its own
    /// section, never counted as a pass. Blaming extraction for material that
    /// reached it truncated would accuse the wrong component, and calling it a
    /// pass would be worse. The coverage table says how many originals of each
    /// profile actually reached extraction, so a profile whose every sample is
    /// inconclusive cannot hide behind a green run.
    ///
    /// Failures accumulate, one original cannot end the run, and the matrix is
    /// written in a finally — a run that stopped at the first bad original
    /// would say nothing about the other eighty, and one that wrote no artifact
    /// would leave a stack trace as its whole evidence.
    ///
    /// Two limits worth naming. Hash equality is asserted case-insensitively
    /// throughout: the pack records lower hex and the asset store upper, and
    /// which casing lands in the candidate row is the reader's choice, not a
    /// claim this test makes. And the Web host composes no
    /// <c>VehicleRegistrationCandidateLookup</c> (it is Worker-only), so this
    /// says nothing about INTK-049 candidate expansion.
    /// </summary>
    [ReferencePackFact]
    [Trait("Category", "Corpus")]
    public async Task GenuineOriginalRetainedAnalysisDiagnosticInventory()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var host = WithAnalysis(factory);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        var root = Top15InstructionCorpusTests.PackRoot();
        var failures = new List<string>();
        var measured = new Dictionary<(string Profile, string Disposition), int>();
        var report = new StringBuilder()
            .AppendLine("# Retained-analysis corpus: fifteen profiles, eighty-one originals")
            .AppendLine()
            .AppendLine(
                CultureInfo.InvariantCulture,
                $"Pack root read from `{Top15InstructionCorpusTests.PackRootVariable}`.")
            .AppendLine(
                "Each original is retained through the ordinary manual-upload path - no route "
                + "activated, no principal confirmed - and analysed through the host's own "
                + "`IAnalyzeRetainedInstruction`. Per-profile ambiguity and missing counts are "
                + "MEASURED below, never asserted.")
            .AppendLine()
            .AppendLine(
                "| Profile | Sample | SHA-256 | Outcome | Principal candidate | Candidates "
                + "| Replay row delta | Cases |")
            .AppendLine("| --- | --- | --- | --- | --- | ---: | ---: | ---: |");

        var inconclusive = new List<string>();
        var analysed = 0;
        var perProfile = new Dictionary<(string Profile, string Bucket), int>();
        try
        {
            foreach (var expectation in Top15InstructionCorpusTests.Expectations)
            {
                var name = Path.GetFileName(expectation.PackRelativePath);
                var absolute = Path.Combine(
                    root,
                    expectation.PackRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolute))
                {
                    failures.Add($"{name}: the pack does not carry this original.");
                    continue;
                }

                // Read once: the bytes hashed here are the bytes uploaded, so
                // what was verified is what was analysed.
                var bytes = await File.ReadAllBytesAsync(absolute);
                var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
                if (!string.Equals(sha256, expectation.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(
                        $"{name}: hashes to {sha256}, and the pack records {expectation.Sha256}.");
                    continue;
                }

                CorpusSample sample;
                try
                {
                    sample = await AnalyseRetainedOriginalAsync(
                        factory, host, client, expectation, bytes, sha256);
                }
                catch (Exception exception) when (exception is ArgumentException
                    or InvalidOperationException
                    or IntakeArtifactIntegrityException)
                {
                    // One original must not end the run. The types named are the
                    // ones this path can actually raise - a policy guard, an
                    // upload that produced nothing to process, a retained asset
                    // that failed its integrity check - so a genuinely unexpected
                    // exception still surfaces as itself rather than being
                    // flattened into a report line.
                    sample = CorpusSample.Threw(name, expectation.Profile, exception);
                }

                failures.AddRange(sample.Failures);
                inconclusive.AddRange(sample.Inconclusive);
                var bucket = sample.Inconclusive.Count > 0
                    ? "Inconclusive"
                    : sample.Failures.Count > 0 ? "Failed" : "Analysed";
                if (bucket == "Analysed")
                {
                    analysed++;
                }

                var profileBucket = (expectation.Profile, bucket);
                perProfile[profileBucket] = perProfile.GetValueOrDefault(profileBucket) + 1;
                report.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"| {expectation.Profile} | {Top15InstructionCorpusTests.Cell(name)} "
                    + $"| {sha256[..12]} | {sample.Outcome} | {sample.PrincipalCandidate} "
                    + $"| {sample.CandidateCount} | {sample.ReplayRowDelta} | {sample.Cases} |");
                foreach (var candidate in sample.Candidates)
                {
                    var key = (expectation.Profile, candidate.Disposition.ToString());
                    measured[key] = measured.GetValueOrDefault(key) + 1;
                }
            }
        }
        finally
        {
            // The matrix is the evidence. It is written whatever happened to the
            // run, because a failed run that leaves nothing behind teaches less
            // than a failed run that says which eighty originals it got through.
            AppendCoverage(report, perProfile);
            AppendMeasuredDispositions(report, measured);
            AppendSection(report, "Inconclusive", inconclusive);
            AppendSection(report, "Failures", failures);
            WriteCorpusReport("retained-analysis-corpus.md", report.ToString());
        }

        Assert.True(
            analysed > 0,
            "No genuine original reached extraction at all, so nothing was proved.");

        // The whole-run allocation claim, read from the tables that would carry
        // one: Cases is the case row itself, CaseIntakeLinks the association
        // automatic allocation writes, and IntakeManualAssociations the staff
        // one. Eighty-one instructions were read and none created work.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            Assert.Equal(0, await context.Cases.CountAsync());
            Assert.Equal(0, await context.CaseIntakeLinks.CountAsync());
            Assert.Equal(0, await context.IntakeManualAssociations.CountAsync());
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Mandatory acceptance gate across all eighty-one originals in the fifteen
    /// Top15 profiles: every genuine original reaches retained analysis,
    /// returns Analyzed, identifies the expected principal candidate, creates
    /// zero Cases or intake links, and produces zero inconclusive or failed
    /// outcomes.
    /// </summary>
    [ReferencePackFact]
    [Trait("Category", "Corpus")]
    public async Task EveryGenuineOriginalReachesRetainedAnalysisWithoutAllocating()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var host = WithAnalysis(factory);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        var root = Top15InstructionCorpusTests.PackRoot();
        var failures = new List<string>();
        var inconclusive = new List<string>();
        var analysed = 0;
        var profilesRepresented = new HashSet<string>(StringComparer.Ordinal);

        foreach (var expectation in Top15InstructionCorpusTests.Expectations)
        {
            var name = Path.GetFileName(expectation.PackRelativePath);
            var absolute = Path.Combine(
                root,
                expectation.PackRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute))
            {
                failures.Add($"{name}: the pack does not carry this original.");
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(absolute);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!string.Equals(sha256, expectation.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{name}: hashes to {sha256}, and the pack records {expectation.Sha256}.");
                continue;
            }

            CorpusSample sample;
            try
            {
                sample = await AnalyseRetainedOriginalAsync(
                    factory, host, client, expectation, bytes, sha256);
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException
                or IntakeArtifactIntegrityException)
            {
                sample = CorpusSample.Threw(name, expectation.Profile, exception);
            }

            failures.AddRange(sample.Failures);
            inconclusive.AddRange(sample.Inconclusive);
            if (sample.Inconclusive.Count == 0 && sample.Failures.Count == 0)
            {
                analysed++;
                profilesRepresented.Add(expectation.Profile);
            }
        }

        await using (var scope = host.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            Assert.Equal(0, await context.Cases.CountAsync());
            Assert.Equal(0, await context.CaseIntakeLinks.CountAsync());
            Assert.Equal(0, await context.IntakeManualAssociations.CountAsync());
        }

        Assert.Empty(failures);
        Assert.Empty(inconclusive);
        Assert.Equal(Top15InstructionCorpusTests.Expectations.Count, analysed);
        Assert.Equal(15, profilesRepresented.Count);
    }

    /// <summary>
    /// One genuine original per non-QDOS profile through the NORMAL intake
    /// path — the same upload and Worker drain production runs — proving the
    /// rule the plan states: nothing is allocated automatically without an
    /// independently accepted route or staff-confirmed principal evidence. The
    /// document alone, however confidently a profile identifies it, is not
    /// enough.
    ///
    /// Fourteen samples, not fifteen. QDOS's automatic allocation belongs to the
    /// accepted mail route and is proved there by <c>QdosIntakeWebTests
    /// .StaffForwardedEmailStrongContentBeatsSenderAndRendersPersistedDraft</c>;
    /// through manual upload no profile allocates, QDOS included.
    ///
    /// State the limit of the negative honestly: a manual upload presents no
    /// transport sender, so <c>EvaluateMailRoute</c> returns null, no principal
    /// context is established, and the assessment terminates at NeedsSorting
    /// before any extraction policy is consulted. This therefore proves that a
    /// confidently identified document does not by itself create work — it does
    /// NOT distinguish that from "this channel never allocates". The sharper
    /// negative the plan describes, a document one profile identifies arriving
    /// through an accepted route for a DIFFERENT principal, belongs to C03/C04.
    ///
    /// "Retained for staff" is asserted as the product records it, not as a
    /// page renders it: an Open item in the Unidentified queue keyed on the
    /// receipt. That is what a member of staff actually finds the material by.
    /// </summary>
    [ReferencePackFact]
    [Trait("Category", "Corpus")]
    public async Task NoGenuineNonQdosOriginalIsAllocatedAutomaticallyThroughNormalIntake()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var root = Top15InstructionCorpusTests.PackRoot();
        var samples = Top15InstructionCorpusTests.Expectations
            .Where(expectation =>
                !string.Equals(expectation.Profile, "QDOS", StringComparison.Ordinal))
            .GroupBy(expectation => expectation.Profile, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        Assert.Equal(14, samples.Length);

        var failures = new List<string>();
        foreach (var expectation in samples)
        {
            var name = Path.GetFileName(expectation.PackRelativePath);
            var absolute = Path.Combine(
                root,
                expectation.PackRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute))
            {
                failures.Add($"{name}: the pack does not carry this original.");
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(absolute);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!string.Equals(sha256, expectation.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{name}: hashes to {sha256}, and the pack records {expectation.Sha256}.");
                continue;
            }

            var upload = await IntakeWebDriver.UploadAndProcessAsync(
                factory,
                client,
                name,
                Top15InstructionCorpusTests.MediaType(name),
                bytes,
                Guid.NewGuid().ToString("N"));
            var receiptId = IntakeWebDriver.ReceiptId(upload);

            await using var scope = factory.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None);
            if (receipt is null)
            {
                failures.Add($"{name}: the upload retained no receipt.");
                continue;
            }

            if (receipt.Decision == IntakeDecision.CaseCreated)
            {
                failures.Add(
                    $"{name} ({expectation.Profile}): the normal intake path decided case_created "
                    + $"with no accepted route and no confirmed principal - {receipt.DecisionReason}");
            }

            if (receipt.AcceptedCaseId is not null
                || receipt.ManualLinkedCaseId is not null
                || receipt.AllocationState is not null)
            {
                failures.Add(
                    $"{name} ({expectation.Profile}): the receipt carries an allocation "
                    + $"(accepted {receipt.AcceptedCaseId}, manual {receipt.ManualLinkedCaseId}).");
            }

            if (IntakeFileIdentity.SourceAsset(receipt) is not { } retained
                || !string.Equals(retained.ContentHash, sha256, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{name} ({expectation.Profile}): the original was not retained intact.");
            }

            var held = await services.GetRequiredService<IUnidentifiedStore>()
                .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None);
            if (held is null || held.State != UnidentifiedState.Open)
            {
                failures.Add(
                    $"{name} ({expectation.Profile}): the material is not held for staff "
                    + $"({(held is null ? "no Unidentified item" : held.State.ToString())}) "
                    + $"after deciding {receipt.Decision}.");
            }
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            Assert.Equal(0, await context.Cases.CountAsync());
            Assert.Equal(0, await context.CaseIntakeLinks.CountAsync());
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// What one original did: the row the matrix prints, and every way it fell
    /// short. Failures are carried rather than thrown so a run reports all
    /// eighty-one originals instead of the first bad one.
    /// </summary>
    private sealed record CorpusSample(
        string Outcome,
        string PrincipalCandidate,
        int CandidateCount,
        int ReplayRowDelta,
        string Cases,
        IReadOnlyList<RetainedInstructionCandidate> Candidates,
        IReadOnlyList<string> Failures,
        IReadOnlyList<string> Inconclusive)
    {
        /// <summary>
        /// An original the retained path could not get through at all. The Cases
        /// cell says "not read" rather than 0: the run never reached the count,
        /// and printing a zero it did not observe would be a claim about the
        /// database nobody made.
        /// </summary>
        public static CorpusSample Threw(string name, string profile, Exception exception) =>
            new(
                "threw",
                "none",
                0,
                0,
                "not read",
                [],
                [$"{name} ({profile}): analysis threw {exception.GetType().Name} - {exception.Message}"],
                []);
    }

    private static async Task<CorpusSample> AnalyseRetainedOriginalAsync(
        IntakeWebApplicationFactory factory,
        WebApplicationFactory<Program> host,
        HttpClient client,
        Top15InstructionCorpusTests.SampleExpectation expectation,
        byte[] bytes,
        string sha256)
    {
        var name = Path.GetFileName(expectation.PackRelativePath);
        var failures = new List<string>();

        // Retention only. The manual-upload path activates no route and
        // confirms no principal, so what the analysis reads is exactly the
        // unresolved retained material this command exists for.
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            name,
            Top15InstructionCorpusTests.MediaType(name),
            bytes,
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        if (receipt is null || IntakeFileIdentity.SourceAsset(receipt) is not { } asset)
        {
            failures.Add($"{name}: the upload retained no single source asset to analyse.");
            return new(
                "not retained",
                "none",
                0,
                0,
                Cell(await context.Cases.CountAsync()),
                [],
                failures,
                []);
        }

        if (!string.Equals(asset.ContentHash, sha256, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{name}: the retained asset hashes to {asset.ContentHash}, "
                + $"and the original is {sha256}.");
        }

        // The command as the host composes it, not a hand-built instance.
        var analyze = services.GetRequiredService<IAnalyzeRetainedInstruction>();
        var operationKey = $"retained-corpus:{receiptId:N}";
        var first = await analyze.ExecuteAsync(
            new(StaffActor(), receiptId, receipt.Version, operationKey),
            CancellationToken.None);
        var analysis = first.Analysis;
        if (first.Outcome != RetainedInstructionAnalysisOutcome.Analyzed || analysis is null)
        {
            // An original the reader could not deliver has given the profile
            // nothing to identify and the policy nothing to extract. Calling
            // that an extraction failure blames the wrong component, so it is
            // recorded INCONCLUSIVE and - in this suite's inherited words -
            // inconclusive is not a pass and is never counted as one. The
            // corroboration is production's own second reading of the same
            // bytes: the receipt the intake pipeline wrote beside it.
            //
            // Everything else is a failure. Ambiguous or NoProfile on an
            // operator-labelled original means the document did not resolve to
            // the profile a labeller read off it, and no expectation row
            // records such an outcome, so none is tolerated.
            var unreadable = CouldNotBeRead(first, receipt);
            var line = $"{name} ({expectation.Profile}): analysis returned {first.Outcome} "
                + $"where the labeller assigned {expectation.Profile} - {first.Reason}";
            return new(
                first.Outcome.ToString(),
                "none",
                analysis?.Candidates.Count ?? 0,
                0,
                Cell(await context.Cases.CountAsync()),
                [],
                unreadable is null ? [.. failures, line] : failures,
                unreadable is null
                    ? []
                    : [$"{line} [{unreadable}] - INCONCLUSIVE, which is not a pass."]);
        }

        var persistedBefore = await context.Set<IntakeSourceCandidateEntity>().AsNoTracking()
            .Where(item => item.AnalysisId == analysis.Id)
            .ToArrayAsync();

        // The replay, under the same key at the same version.
        var second = await analyze.ExecuteAsync(
            new(StaffActor(), receiptId, receipt.Version, operationKey),
            CancellationToken.None);
        var persistedAfter = await context.Set<IntakeSourceCandidateEntity>().AsNoTracking()
            .CountAsync(item => item.AnalysisId == analysis.Id);

        if (!string.Equals(
            analysis.SourceSha256, asset.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{name}: the analysis recorded source {analysis.SourceSha256} "
                + $"over retained {asset.ContentHash}.");
        }

        if (analysis.Candidates.Count == 0)
        {
            failures.Add($"{name}: the analysis recorded no candidates at all.");
        }

        var principal = ProposedPrincipal(analysis, expectation, name, failures);

        // Every persisted row is keyed on the immutable source, and the
        // occurrences read back are the ones the analysis recorded.
        var wrongSource = persistedBefore
            .Where(row => !string.Equals(
                row.SourceSha256, asset.ContentHash, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (wrongSource.Length > 0)
        {
            failures.Add(
                $"{name}: {wrongSource.Length} persisted candidates carry a source hash that is "
                + $"not the retained original's {asset.ContentHash}.");
        }

        var recorded = analysis.Candidates
            .Select(candidate => $"{candidate.Field}#{candidate.Occurrence}")
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        var stored = persistedBefore
            .Select(row => $"{row.Field}#{row.Occurrence}")
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (!recorded.SequenceEqual(stored, StringComparer.Ordinal))
        {
            failures.Add(
                $"{name}: the persisted rows are not the candidates the analysis recorded "
                + $"({stored.Length} stored against {recorded.Length} recorded).");
        }

        if (!second.IsReplay
            || second.Analysis?.Id != analysis.Id
            || persistedAfter != persistedBefore.Length)
        {
            failures.Add(
                $"{name}: the second execution under the same key was not a replay "
                + $"(replay {second.IsReplay}, rows {persistedBefore.Length} then {persistedAfter}).");
        }

        // Analysis proposes; it never decides. The receipt is where a decision
        // would show, and it has not moved.
        var after = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        if (after is null
            || after.Version != receipt.Version
            || after.AcceptedCaseId is not null
            || after.ManualLinkedCaseId is not null
            || after.AllocationState is not null)
        {
            failures.Add($"{name}: analysis moved the receipt or allocated against it.");
        }

        var caseCount = await context.Cases.CountAsync();
        if (caseCount != 0 || await context.CaseIntakeLinks.AnyAsync())
        {
            failures.Add($"{name}: analysing this original left {caseCount} Cases behind.");
        }

        return new(
            first.Outcome.ToString(),
            principal,
            analysis.Candidates.Count,
            persistedAfter - persistedBefore.Length,
            Cell(caseCount),
            analysis.Candidates,
            failures,
            []);
    }

    /// <summary>
    /// Why the reader could not deliver this original, or null where it could.
    ///
    /// The command reports <see cref="RetainedInstructionAnalysisOutcome.SourceUnavailable"/>
    /// for a source it could not open or could not read completely, and carries
    /// the reader's own account of what is missing. The receipt adds the second
    /// half the command cannot see: a PDF whose pages hold too little embedded
    /// text reads as complete and simply matches nothing, and the intake
    /// pipeline - reading the same bytes with the same reader - recorded that as
    /// evidence when the material was retained.
    /// </summary>
    private static string? CouldNotBeRead(
        AnalyzeRetainedInstructionResult result,
        IntakeReceipt receipt)
    {
        if (result.Outcome == RetainedInstructionAnalysisOutcome.SourceUnavailable)
        {
            return "the retained source could not be read";
        }

        if (receipt.Decision == IntakeDecision.OcrRequired || receipt.ScannedPdfPages.Count > 0)
        {
            return "the retained source needs text review before it can be read";
        }

        var lowText = receipt.Evidence.FirstOrDefault(evidence =>
            evidence.Signal is "insufficient-embedded-text" or "scanned-pdf-page");
        return lowText is null ? null : $"the reader recorded {lowText.Signal}";
    }

    private static string Cell(int count) => count.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The principal the document proposed, checked as a PROPOSAL. There is no
    /// confirmed disposition to test for — <see cref="SourceCandidateDisposition"/>
    /// has none — so "never confirmed" is proved by what the row is and where it
    /// lives: one candidate under
    /// <see cref="AnalyzeRetainedInstruction.SuggestedPrincipalField"/>, in the
    /// candidate table, in the review-only disposition production emits, beside
    /// a receipt that carries no allocation.
    /// </summary>
    private static string ProposedPrincipal(
        RetainedInstructionAnalysis analysis,
        Top15InstructionCorpusTests.SampleExpectation expectation,
        string name,
        List<string> failures)
    {
        var proposed = analysis.Candidates
            .Where(candidate =>
                candidate.Field == AnalyzeRetainedInstruction.SuggestedPrincipalField)
            .ToArray();
        if (proposed.Length != 1)
        {
            failures.Add(
                $"{name}: the analysis recorded {proposed.Length} proposed principals, "
                + "and a matched document proposes exactly one.");
            return "none";
        }

        var principal = proposed[0];
        if (!string.Equals(principal.RawValue, expectation.Profile, StringComparison.Ordinal))
        {
            failures.Add(
                $"{name}: the document proposed {principal.RawValue}, and the labeller "
                + $"assigned it {expectation.Profile}.");
        }

        if (principal.PartyRole != AnalyzeRetainedInstruction.PrincipalPartyRole)
        {
            failures.Add(
                $"{name}: the proposed principal carries party role '{principal.PartyRole}'.");
        }

        // Usable exactly, not "one of the review-only dispositions". On this
        // path the disposition is `forceReviewOnly ? Ambiguous : Usable`
        // (AnalyzeRetainedInstruction.cs:516) and nothing passes forceReviewOnly
        // true - it is a defaulted parameter with no call site - so Ambiguous is
        // unreachable here and tolerating it would read as a known-failure
        // allowance that no production behaviour needs.
        if (principal.Disposition != SourceCandidateDisposition.Usable)
        {
            failures.Add(
                $"{name}: the proposed principal was recorded {principal.Disposition} "
                + "rather than Usable.");
        }

        return $"{principal.RawValue} ({principal.Disposition})";
    }

    /// <summary>
    /// How far each profile actually got. The count that matters is Analysed:
    /// a profile whose every sample is Inconclusive has not been shown to reach
    /// extraction at all, and a matrix that only totalled dispositions would
    /// hide that behind a page of zeroes.
    /// </summary>
    private static void AppendCoverage(
        StringBuilder report,
        Dictionary<(string Profile, string Bucket), int> perProfile)
    {
        report.AppendLine()
            .AppendLine("## Coverage by profile")
            .AppendLine()
            .AppendLine("| Profile | Analysed | Inconclusive | Failed |")
            .AppendLine("| --- | ---: | ---: | ---: |");
        foreach (var profile in perProfile.Keys
            .Select(key => key.Profile)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(profile => profile, StringComparer.Ordinal))
        {
            report.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {profile} | {perProfile.GetValueOrDefault((profile, "Analysed"))} "
                + $"| {perProfile.GetValueOrDefault((profile, "Inconclusive"))} "
                + $"| {perProfile.GetValueOrDefault((profile, "Failed"))} |");
        }
    }

    private static void AppendSection(
        StringBuilder report,
        string heading,
        List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        report.AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"## {heading}")
            .AppendLine();
        foreach (var line in lines)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"- {line}");
        }
    }

    private static void AppendMeasuredDispositions(
        StringBuilder report,
        Dictionary<(string Profile, string Disposition), int> measured)
    {
        report.AppendLine()
            .AppendLine("## Measured dispositions")
            .AppendLine()
            .AppendLine(
                "Counted, not asserted. Five samples per principal prove examples, not "
                + "production accuracy.")
            .AppendLine()
            .AppendLine("| Profile | Usable | Ambiguous | Missing | Conflicting |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var profile in measured.Keys
            .Select(key => key.Profile)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(profile => profile, StringComparer.Ordinal))
        {
            report.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {profile} "
                + $"| {measured.GetValueOrDefault((profile, nameof(SourceCandidateDisposition.Usable)))} "
                + $"| {measured.GetValueOrDefault((profile, nameof(SourceCandidateDisposition.Ambiguous)))} "
                + $"| {measured.GetValueOrDefault((profile, nameof(SourceCandidateDisposition.Missing)))} "
                + $"| {measured.GetValueOrDefault((profile, nameof(SourceCandidateDisposition.Conflicting)))} |");
        }
    }

    private static void WriteCorpusReport(string fileName, string content)
    {
        var directory = Path.Combine(
            CorpusPackage.RepositoryRoot, "artifacts", "evaluation", "v1-intake");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, fileName), content, new UTF8Encoding(false));
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
    /// The production composition, plus — in this tree only — the one port it
    /// does not compose.
    ///
    /// Stream A's <c>AddPegasusInfrastructure</c> registers the fifteen
    /// extraction policies, the selector, the analysis store, the candidate
    /// queries and both <c>AnalyzeRetainedInstruction</c> registrations, so
    /// these tests resolve the command the host itself resolves rather than a
    /// hand-composed copy that could pass while production could not build the
    /// graph at all.
    ///
    /// The exception is <see cref="IReadLogicalDocumentVersion"/>. A04's
    /// concrete reader is Stream A's — <c>LocalLogicalDocumentVersionReader</c>,
    /// registered by A's own <c>DependencyInjection</c> — and it is NOT present
    /// in this standalone C tree, so nothing here could resolve the command
    /// without a stand-in. <c>TryAdd</c>, not <c>Add</c>, is what makes that
    /// sentence true rather than aspirational: where the host composes A's real
    /// reader, as the combined tree does, this registration does nothing and
    /// the proof runs against the production reader; only where no host
    /// composes one does the C-owned stand-in below fill the port. Read a green
    /// run in the standalone tree accordingly — it proves everything except the
    /// reader port.
    /// </summary>
    private static WebApplicationFactory<Program> WithAnalysis(
        IntakeWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.TryAddScoped<IReadLogicalDocumentVersion, RetainedIntakeAssetReader>()));

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
