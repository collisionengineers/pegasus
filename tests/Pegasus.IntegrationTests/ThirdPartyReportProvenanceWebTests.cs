using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Infrastructure.Persistence;
using Pegasus.IntegrationTests.Support;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A genuine third-party report, uploaded through the real web host: retention
/// identifies the document's role, and the report reader records what it says
/// with the source locator of every value.
///
/// Nothing here is a stand-in for the pipeline. The upload goes through the
/// real page, the bytes are retained through the real artifact store, the text
/// is read by the real reader and the candidates are written by the real EF
/// store. The only test-only composition is the logical-document reader, which
/// is stated where it happens rather than hidden behind an optional dependency
/// that quietly does nothing.
/// </summary>
[Trait("Category", "Corpus")]
[Trait("Category", "SqlServer")]
public sealed class ThirdPartyReportProvenanceWebTests
{
    /// <summary>
    /// The Montgomery original: its printed hours-times-rate contradiction and
    /// its reconciling totals make it the case where showing every printed
    /// value, rather than a repaired one, actually matters.
    /// </summary>
    private const string ReportName = "MontgomeryRepairable1.pdf";

    [ReferencePackFact]
    public async Task AnUploadedReportIsRecordedAsSourceCandidates()
    {
        var (bytes, hash) = ReadOriginal(ReportName);
        using var factory = new IntakeWebApplicationFactory();
        using var host = WithSourceCandidates(factory);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            host,
            client,
            ReportName,
            "application/pdf",
            bytes,
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(receipt);
        Assert.Equal(hash, receipt.SourceHash);

        // The candidates are read back from the recorded rows — not through
        // the writing path that produced them.
        var candidates = await ReadCandidatesAsync(
            services,
            receiptId,
            IntakeFileIdentity.SourceAsset(receipt)!.Id);

        Assert.NotEmpty(candidates);

        // Every row names the versioned policy that read it, so a later change
        // to the rules is distinguishable from this reading. A finding row is
        // stamped with the finding rules' own version, so a change to the
        // arithmetic is distinguishable from a change to the reading.
        Assert.All(
            candidates,
            row => Assert.Contains(
                row.PolicyVersion,
                new[]
                {
                    ThirdPartyReportProfiles.ProfileVersion,
                    ThirdPartyReportExtraction.ProfileVersion,
                    ThirdPartyReportValidation.PolicyVersion
                }));

        // The issuer was read from the document, and the document's role says
        // it is a third-party engineer report.
        var issuer = Assert.Single(
            candidates,
            row => row.Field == ThirdPartyReportFields.Issuer);
        Assert.Equal("Montgomery Assessors", issuer.NormalizedValue);
        Assert.Equal(SourceCandidateDisposition.Usable, issuer.Disposition);
        Assert.All(
            candidates,
            row => Assert.Equal(
                ThirdPartyReportProfiles.ReportDocumentRole,
                row.DocumentRole));

        // Every persisted row keeps the source identity and the locator that
        // lets an operator open the page it was read from.
        Assert.All(candidates, row => Assert.Equal(hash, row.Sha256));
        Assert.All(candidates, row => Assert.NotNull(row.IntakeAssetId));
        Assert.All(candidates, row => Assert.False(string.IsNullOrWhiteSpace(row.SourceLabel)));
        Assert.Contains(
            candidates,
            row => row.Disposition == SourceCandidateDisposition.Usable
                && row.Page is not null
                && row.Field != ThirdPartyReportFields.Issuer);

        // The printed amount roles stayed separate on the way to storage.
        Assert.Contains(
            candidates,
            row => row.Field == ThirdPartyReportFields.LabourAmount
                && row.ReferenceRole == "assessed"
                && row.NormalizedValue == "1582.20"
                && row.Currency == "GBP");

        // The printed contradiction reached storage as its own row. It is not
        // a value and cannot be mistaken for one: its field is namespaced, its
        // normalized value is the finding code, and its raw text is the
        // statement an operator reads.
        var mismatch = Assert.Single(
            candidates,
            row => row.Field == ThirdPartyReportFields.Finding(
                ThirdPartyFindingCodes.LabourHoursRateMismatch));
        Assert.Equal(ThirdPartyFindingCodes.LabourHoursRateMismatch, mismatch.NormalizedValue);
        Assert.Equal(SourceCandidateDisposition.Conflicting, mismatch.Disposition);
        Assert.Equal("assessed", mismatch.ReferenceRole);
        Assert.Equal(ThirdPartyReportValidation.PolicyVersion, mismatch.PolicyVersion);

        // Both printed values it compares are named in the statement itself.
        Assert.Contains("26.2 hours at 90", mismatch.RawValue!, StringComparison.Ordinal);
        Assert.Contains("not the printed labour 1582.2", mismatch.RawValue!, StringComparison.Ordinal);

        // And the three rows it compares still carry exactly what the document
        // printed. Nothing wrote the arithmetic's answer back as a source value.
        Assert.Equal("26.20", Normalized(candidates, ThirdPartyReportFields.LabourHours, "assessed"));
        Assert.Equal("90.00", Normalized(candidates, ThirdPartyReportFields.LabourRate, "assessed"));
        Assert.Equal("1582.20", Normalized(candidates, ThirdPartyReportFields.LabourAmount, "assessed"));
        Assert.DoesNotContain(
            candidates,
            row => row.Field == ThirdPartyReportFields.LabourAmount
                && row.NormalizedValue == "2358.00");

        // The two reconciliations that do hold are recorded beside it, so the
        // contradiction is read in the context of the figures that agree.
        Assert.Contains(
            candidates,
            row => row.Field == ThirdPartyReportFields.Finding(
                ThirdPartyFindingCodes.ComponentSumReconciles));
        Assert.Contains(
            candidates,
            row => row.Field == ThirdPartyReportFields.Finding(
                ThirdPartyFindingCodes.NetVatGrossReconciles));

        // And C wrote no Engineer or CE value anywhere: no Case exists, and the
        // receipt carries no accepted case.
        Assert.Null(receipt.AcceptedCaseId);
        Assert.Null(receipt.CurrentCaseId);
    }

    /// <summary>
    /// A staff re-evaluation of a receipt whose reading is already recorded
    /// leaves that reading exactly as it was: the same rows, the same
    /// identifiers, no second set of candidates — and it gets there by actually
    /// reading the source again, not by failing before it reaches the reader.
    ///
    /// This is the retargeted case. The earlier round pinned the outcome of a
    /// gap: the re-claimed pass read the staged copy, which a completed receipt
    /// no longer has, so it failed with <c>staged_artifact_integrity_failure</c>
    /// before intake ran at all, and only one pass ever tagged a third-party
    /// outcome (recorded on the ticket as ASSUMPTION 8). Stream A closed that
    /// gap in the durable intake path: a queued re-evaluation now
    /// re-reads the exact retained source through
    /// <see cref="IReadLogicalDocumentVersion"/>, by identity, against the
    /// recorded hash and length. So the pass completes, reads the report again,
    /// and the store refuses the second write under the same asset-derived
    /// operation key — the conflict <c>ProcessIntake</c> reports as
    /// <c>recorded_reading_stands</c>. Two outcomes, in that order, one set of
    /// candidate rows.
    ///
    /// The reader is the one thing here that is not the production object.
    /// Standalone C composes no concrete reader — A04's adapters are A-owned
    /// and are supplied by the combined host — so this test registers a C-owned
    /// double that serves the retained source's exact bytes for this receipt's
    /// logical version and refuses anything else, and asserts what the pass
    /// asked it for. That is qualified boundary proof: it does not make C carry
    /// A04's adapters, and no production fallback stands behind it. A's own
    /// tests prove the real local reader and the Box/cache Worker path.
    /// </summary>
    [ReferencePackFact]
    public async Task AQueuedReevaluationLeavesTheRecordedReadingExactlyAsItWas()
    {
        var (bytes, hash) = ReadOriginal(ReportName);

        // Every outcome is kept with the receipt it belongs to.
        // ActivitySource.AddActivityListener is process-global, so a collection
        // running beside this one tags outcomes on this listener too; filtering
        // by receipt is what makes the assertions below about this test's own
        // passes.
        var outcomes = new ConcurrentQueue<(Guid Receipt, string Outcome)>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Pegasus.Core.Intake",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = stopped =>
            {
                if (stopped.GetTagItem("intake.third_party_report.outcome") is string outcome
                    && stopped.GetTagItem("intake.receipt_id") is Guid tagged)
                {
                    outcomes.Enqueue((tagged, outcome));
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        using var factory = new IntakeWebApplicationFactory();

        // Armed below, once retention has given the source the identity, hash
        // and length the queued pass will ask for. Until then it refuses, so
        // the first pass cannot quietly read through it.
        var retainedReader = new RecordingLogicalDocumentVersionReader();
        using var host = WithSourceCandidates(factory, retainedReader);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        var token = Guid.NewGuid().ToString("N");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            host,
            client,
            ReportName,
            "application/pdf",
            bytes,
            token);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receiptQueries = services.GetRequiredService<IIntakeReceiptQueries>();
        var receipt = await receiptQueries.GetAsync(receiptId, CancellationToken.None);
        var assetId = IntakeFileIdentity.SourceAsset(receipt!)!.Id;
        var first = await ReadCandidatesAsync(services, receiptId, assetId);
        Assert.NotEmpty(first);

        // The first pass read the staged copy it was handed, so nothing has
        // asked the logical reader anything yet. Asserted, because everything
        // below is about the one request that follows.
        Assert.Empty(retainedReader.Requests);

        // Re-evaluate the retained source. The command queues the work; the
        // dispatcher below stands in for the Worker timer and runs the pass
        // production would run.
        await services.GetRequiredService<IReevaluateIntake>().ExecuteAsync(
            new(
                receiptId,
                receipt!.Version,
                StaffActor(),
                $"reevaluate:{receiptId:N}",
                "Re-reading the retained third-party report."));

        // What the queued pass must re-read, taken from the receipt as it
        // stands now — the same asset, still holding the bytes this test
        // uploaded, which is what makes serving them here "the exact retained
        // source" rather than a convenient stand-in.
        var reevaluated = await receiptQueries.GetAsync(receiptId, CancellationToken.None);
        var retained = IntakeFileIdentity.SourceAsset(reevaluated!)!;
        Assert.Equal(assetId, retained.Id);
        Assert.Equal(hash, retained.ContentHash, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(bytes.LongLength, retained.ContentLength);
        retainedReader.Serve(new(
            retained.Id,
            reevaluated!.CurrentCaseId,
            reevaluated.Id,
            retained.ContentHash,
            retained.FileName,
            retained.MediaType,
            bytes));

        var dispatcher = new DispatchPendingIntakeWork(
            services.GetRequiredService<IIntakeWorkStore>(),
            new IntakeWebDriver.ImmediateIntakeWorkEnqueuer(
                IntakeWebDriver.CreateProcessor(services)),
            services.GetRequiredService<TimeProvider>());
        Assert.Equal(1, await dispatcher.ExecuteAsync(1, CancellationToken.None));

        // The pass re-read the retained source through the port, once, by
        // identity: no storage key, the receipt's own logical version, and the
        // recorded hash and length as the expectation. A double that had been
        // asked for anything else would have refused instead of serving.
        var asked = Assert.Single(retainedReader.Requests);
        Assert.Null(asked.DocumentId);
        Assert.Null(asked.VersionId);
        Assert.Equal(retained.Id, asked.IntakeAssetId);
        Assert.Equal(reevaluated.CurrentCaseId, asked.CaseId);
        Assert.Equal(receiptId, asked.IntakeReceiptId);
        Assert.Equal(retained.ContentHash, asked.ExpectedSha256, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(retained.ContentLength, asked.ExpectedContentLength);

        // Where the queued pass got to, read from the durable work item rather
        // than inferred: it completed, so it reached the reader, the
        // third-party gate and the analysis store.
        var stagedReceiptId = await services.GetRequiredService<IIntakeWorkStore>()
            .FindStagedReceiptIdForReceiptAsync(receiptId, CancellationToken.None);
        var status = await services.GetRequiredService<IQueuedIntakeStatusQueries>()
            .GetAsync(stagedReceiptId!.Value, CancellationToken.None);
        Assert.Equal(QueuedIntakeStatusKind.Complete, status!.Status);
        Assert.Null(status.FailureCode);

        // Two passes read this source and each says what it did with the
        // reading: the first recorded it, the second found the recorded reading
        // standing and left it alone. A swallowed failure, or a pass that never
        // reached the reader, would leave the same rows behind; the outcome is
        // what tells those apart, so the whole observed sequence is asserted
        // rather than its length.
        var recorded = outcomes
            .Where(entry => entry.Receipt == receiptId)
            .Select(entry => entry.Outcome)
            .ToList();
        string[] expected = ["recorded", "recorded_reading_stands"];
        Assert.Equal(expected, recorded);

        // And the reading itself is untouched: one candidate set, the same rows
        // with the same identifiers, value for value.
        var second = await ReadCandidatesAsync(services, receiptId, assetId);
        Assert.Equal(first.Count, second.Count);
        Assert.Equal(
            first.OrderBy(row => row.Id),
            second.OrderBy(row => row.Id));
    }

    /// <summary>
    /// The other half of the same guarantee, at the boundary that enforces it:
    /// re-recording one document's reading under its operation key never writes
    /// a second set of candidates. The identical request replays the stored
    /// analysis untouched (what <c>ProcessIntake</c> reports as "replayed");
    /// the same request against a moved receipt version — what a genuine second
    /// pass over one asset presents, because every re-evaluation moves the
    /// version — is refused, and that refusal is the conflict
    /// <c>ProcessIntake</c> reports as "recorded_reading_stands".
    ///
    /// Both are exercised against the real store and SQL Server, on the rows a
    /// real report produced, because the claim is about what the database
    /// enforces rather than about what the use case intends.
    /// </summary>
    [ReferencePackFact]
    public async Task RecordingTheSameReadingAgainReplaysItAndAMovedVersionIsRefused()
    {
        var (bytes, _) = ReadOriginal(ReportName);
        using var factory = new IntakeWebApplicationFactory();
        using var host = WithSourceCandidates(factory);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            host,
            client,
            ReportName,
            "application/pdf",
            bytes,
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var assetId = IntakeFileIdentity.SourceAsset(receipt!)!.Id;
        var first = await ReadCandidatesAsync(services, receiptId, assetId);
        Assert.NotEmpty(first);

        // The key the retention pass recorded under, derived from the asset.
        var store = services.GetRequiredService<IRetainedInstructionAnalysisStore>();
        var stored = await store.FindByOperationKeyAsync(
            $"{ThirdPartyReportAnalysis.PolicyKey}:{assetId}",
            CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(receiptId, stored!.ReceiptId);
        Assert.Equal(assetId, stored.IntakeAssetId);

        // The identical request replays the stored reading and writes nothing.
        var (replayed, isReplay) = await store.RecordAsync(
            stored with { Id = Guid.NewGuid() },
            CancellationToken.None);
        Assert.True(isReplay);
        Assert.Equal(stored.Id, replayed.Id);

        // A moved receipt version is refused rather than overwritten.
        await Assert.ThrowsAsync<RetainedInstructionAnalysisConflictException>(
            () => store.RecordAsync(
                stored with
                {
                    Id = Guid.NewGuid(),
                    ExpectedReceiptVersion = stored.ExpectedReceiptVersion + 1
                },
                CancellationToken.None));

        // Neither attempt added, replaced or removed a candidate.
        var after = await ReadCandidatesAsync(services, receiptId, assetId);
        Assert.Equal(
            first.Select(row => row.Id).OrderBy(id => id),
            after.Select(row => row.Id).OrderBy(id => id));
    }

    /// <summary>
    /// The normalized value of one printed field in one printed amount role,
    /// or null where the document does not state it.
    /// </summary>
    private static string? Normalized(
        IReadOnlyList<SourceFieldCandidate> candidates,
        string field,
        string referenceRole)
    {
        var row = candidates.FirstOrDefault(candidate =>
            candidate.Field == field && candidate.ReferenceRole == referenceRole);
        return row is null || row.Disposition == SourceCandidateDisposition.Missing
            ? null
            : row.NormalizedValue;
    }

    /// <summary>
    /// The recorded candidates of one retained asset, read straight from the
    /// analysis rows in the order they were recorded. A pre-case candidate has
    /// no Case document, so its document id stays null.
    /// </summary>
    private static async Task<IReadOnlyList<SourceFieldCandidate>> ReadCandidatesAsync(
        IServiceProvider services,
        Guid receiptId,
        Guid intakeAssetId)
    {
        await using var context = await services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var rows = await (
            from candidate in context.Set<IntakeSourceCandidateEntity>().AsNoTracking()
            join analysis in context.Set<RetainedInstructionAnalysisEntity>().AsNoTracking()
                on candidate.AnalysisId equals analysis.Id
            where analysis.IntakeReceiptId == receiptId
                && candidate.IntakeAssetId == intakeAssetId
            orderby analysis.CompletedAtUtc, candidate.Field, candidate.Occurrence
            select candidate)
            .ToArrayAsync();

        return rows.Select(row =>
        {
            var (sourceLabel, page, locator) = AnalyzeRetainedInstruction.ReadLocator(row.LocatorJson);
            return new SourceFieldCandidate(
                row.Id,
                receiptId,
                DocumentId: null,
                row.DocumentVersionId,
                row.IntakeAssetId,
                row.SourceSha256,
                row.Occurrence,
                row.DocumentRole,
                row.PartyRole ?? string.Empty,
                row.ReferenceRole ?? string.Empty,
                row.Field,
                row.RawValue,
                row.NormalizedValue,
                row.Unit,
                row.Currency,
                sourceLabel,
                page,
                locator?.Cell,
                locator?.FormField,
                locator?.Region,
                row.ReaderVersion,
                row.PolicyVersion,
                Enum.Parse<SourceCandidateDisposition>(row.Disposition));
        }).ToArray();
    }

    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    /// <summary>
    /// Composes the retained-analysis store the retention pass records a
    /// report's reading through, and the command that analyses it.
    ///
    /// The logical-document reader is A04's port, which standalone C composes
    /// nowhere: A owns the concrete adapters and the combined host supplies
    /// them. A test that does not re-read a retained source therefore gets a
    /// reader that refuses everything, so a scenario that quietly began to
    /// depend on one fails by name; a test whose scenario does re-read passes
    /// the double it means to exercise, and that double is qualified boundary
    /// proof rather than a claim that C carries A04's adapters.
    /// </summary>
    private static WebApplicationFactory<Program> WithSourceCandidates(
        IntakeWebApplicationFactory factory,
        IReadLogicalDocumentVersion? retainedReader = null) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<EfRetainedInstructionAnalysisStore>();
            services.AddScoped<IRetainedInstructionAnalysisStore>(provider =>
                provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
            services.AddScoped<InstructionExtractionPolicySelector>();
            services.AddSingleton<IReadLogicalDocumentVersion>(
                retainedReader ?? RecordingLogicalDocumentVersionReader.Refusing());
            services.AddScoped<AnalyzeRetainedInstruction>();
        }));

    private static (byte[] Bytes, string Hash) ReadOriginal(string name)
    {
        var root = PrincipalSourceManifestTests.ConfiguredPackRoot()
            ?? throw new InvalidOperationException("This test should have been skipped.");
        var inventory = Path.Combine(
            root, "astra_output", "reports", "third-party-source-inventory.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(inventory));
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var relative = entry.GetProperty("source").GetString()!;
            if (!Path.GetFileName(relative).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            var bytes = File.ReadAllBytes(path);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));

            // The pack records the hash it reviewed. Reading different bytes
            // would prove something about a document nobody examined.
            Assert.Equal(
                entry.GetProperty("sha256").GetString(),
                hash.ToLowerInvariant());
            return (bytes, hash);
        }

        throw new FileNotFoundException($"The pack inventory does not list {name}.");
    }
}
