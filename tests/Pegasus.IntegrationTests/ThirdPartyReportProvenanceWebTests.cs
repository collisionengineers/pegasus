using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
/// identifies the document's role, the report reader records what it says, and
/// the Received screen shows those values with their source locators.
///
/// Nothing here is a stand-in for the pipeline. The upload goes through the
/// real page, the bytes are retained through the real artifact store, the text
/// is read by the real reader, the candidates are written by the real EF store
/// and the screen is the real Razor page. The only test-only composition is the
/// registration Stream A owns (C-F02) and has not landed yet, which is stated
/// where it happens rather than hidden behind an optional dependency that
/// quietly does nothing.
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
    public async Task AnUploadedReportReachesTheReceivedScreenAsSourceCandidates()
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

        // The candidates are read back through the contract the Case surfaces
        // read — not through the writing path that produced them.
        var candidates = await services.GetRequiredService<ISourceCandidateQueries>()
            .GetAsync(
                StaffActor(),
                receiptId,
                documentVersionId: null,
                intakeAssetId: IntakeFileIdentity.SourceAsset(receipt)!.Id,
                CancellationToken.None);

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

        var reports = await services.GetRequiredService<IThirdPartyReportCandidateQueries>()
            .GetAsync(
                StaffActor(),
                receiptId,
                documentVersionId: null,
                intakeAssetId: IntakeFileIdentity.SourceAsset(receipt)!.Id,
                CancellationToken.None);
        var report = Assert.Single(reports);
        Assert.Equal(receiptId, report.Identity.Issuer!.Source.ReceiptId);
        Assert.Equal(hash, report.Sha256);
        Assert.Equal(IntakeFileIdentity.SourceAsset(receipt)!.Id, report.IntakeAssetId);
        Assert.Null(report.DocumentId);
        Assert.Null(report.DocumentVersionId);
        Assert.Equal("Montgomery Assessors", report.Identity.Issuer.Value);
        var assessed = Assert.Single(
            report.Estimates,
            estimate => estimate.Role == ThirdPartyEstimateRole.Assessed);
        Assert.Equal("1582.20", assessed.LabourAmount!.Source.NormalizedValue);
    }

    [ReferencePackFact]
    public async Task TheReceivedScreenShowsTheReportValuesWithTheirSourceLocators()
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

        var page = await client.GetAsync(new Uri($"/Received/{receiptId:D}", UriKind.Relative));
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();

        // The existing provenance projection renders them: the field, the
        // printed value, the disposition in the operator's words, and the
        // source label with its page.
        Assert.Contains(ThirdPartyReportFields.LabourAmount, html, StringComparison.Ordinal);
        Assert.Contains("1,582.20", html, StringComparison.Ordinal);
        Assert.Contains("Usable", html, StringComparison.Ordinal);
        Assert.Contains("page 2", html, StringComparison.Ordinal);

        // A field the document does not state is shown as unstated rather than
        // being filled in or hidden.
        Assert.Contains("Not stated in the document", html, StringComparison.Ordinal);

        // The finding is on the screen too, in the operator's words and beside
        // the printed values it compares. Its namespaced field name is what
        // distinguishes it from a printed value on the row list.
        Assert.Contains(
            ThirdPartyReportFields.Finding(ThirdPartyFindingCodes.LabourHoursRateMismatch),
            html,
            StringComparison.Ordinal);
        Assert.Contains("26.2 hours at 90", html, StringComparison.Ordinal);
        Assert.Contains("not the printed labour 1582.2", html, StringComparison.Ordinal);
        Assert.Contains("Conflicting statements", html, StringComparison.Ordinal);

        // The reconciliations that hold are shown as well: the screen shows the
        // whole reconciliation, not only its failures.
        Assert.Contains(
            ThirdPartyReportFields.Finding(ThirdPartyFindingCodes.ComponentSumReconciles),
            html,
            StringComparison.Ordinal);

        // The panel is composed, so the screen is showing candidates rather
        // than the "not available in this environment" notice.
        Assert.DoesNotContain(
            "The retained-instruction analysis is not available",
            html,
            StringComparison.Ordinal);
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
    /// gap in the durable intake path (INTK-027): a queued re-evaluation now
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
        var queries = services.GetRequiredService<ISourceCandidateQueries>();
        var first = await queries.GetAsync(
            StaffActor(), receiptId, null, assetId, CancellationToken.None);
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
        var second = await queries.GetAsync(
            StaffActor(), receiptId, null, assetId, CancellationToken.None);
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
        var queries = services.GetRequiredService<ISourceCandidateQueries>();
        var first = await queries.GetAsync(
            StaffActor(), receiptId, null, assetId, CancellationToken.None);
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
        var after = await queries.GetAsync(
            StaffActor(), receiptId, null, assetId, CancellationToken.None);
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

    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    /// <summary>
    /// Composes what the Received screen needs to read a recorded analysis.
    /// These registrations are Stream A's to add to <c>DependencyInjection.cs</c>
    /// under C-F02; until they land, the store resolves only here, and
    /// <c>ProcessIntake</c>'s optional dependency stays null in production.
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
            services.AddScoped<ISourceCandidateQueries>(provider =>
                provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
            services.AddScoped<IThirdPartyReportCandidateQueries>(provider =>
                provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
            services.AddScoped<IGetLatestRetainedInstructionAnalysis,
                GetLatestRetainedInstructionAnalysis>();
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
