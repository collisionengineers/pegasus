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
    /// Re-evaluating the same retained bytes re-reads them through the same
    /// reader and leaves the recorded reading exactly as it was — and the span
    /// says which path did that, so a replay is no longer indistinguishable
    /// from a reading that threw and was swallowed. Before the outcome was
    /// named, this test passed either way.
    ///
    /// The re-evaluation command queues the work the Worker picks up; it does
    /// not itself re-read anything. Scheduling and stopping there would prove
    /// only that nothing ran, so this drives the pass that production runs:
    /// the queued processor, over the retained artifact, against the receipt
    /// the first pass produced.
    /// </summary>
    [ReferencePackFact]
    public async Task ReprocessingTheSameRetainedBytesDoesNotWriteASecondSetOfCandidates()
    {
        var (bytes, _) = ReadOriginal(ReportName);

        // Every outcome is kept with the receipt it belongs to.
        // ActivitySource.AddActivityListener is process-global, so a collection
        // running beside this one tags outcomes on this listener too; filtering
        // by receipt is what makes the assertions below about this test's own
        // two passes.
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
        using var host = WithSourceCandidates(factory);
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
        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var assetId = IntakeFileIdentity.SourceAsset(receipt!)!.Id;
        var queries = services.GetRequiredService<ISourceCandidateQueries>();
        var first = await queries.GetAsync(
            StaffActor(), receiptId, null, assetId, CancellationToken.None);

        // Re-evaluate the retained source. The command queues the work; the
        // dispatcher below stands in for the Worker timer and runs the pass
        // that actually re-reads the retained bytes, exactly as production
        // does. The operation key is derived from the asset, and the retained
        // source asset keeps its identity across a re-evaluation, so the
        // recorded reading is left standing instead of being written twice or
        // overwritten.
        await services.GetRequiredService<IReevaluateIntake>().ExecuteAsync(
            new(
                receiptId,
                receipt!.Version,
                StaffActor(),
                $"reevaluate:{receiptId:N}",
                "Re-reading the retained third-party report."));
        var dispatcher = new DispatchPendingIntakeWork(
            services.GetRequiredService<IIntakeWorkStore>(),
            new IntakeWebDriver.ImmediateIntakeWorkEnqueuer(
                IntakeWebDriver.CreateProcessor(services)),
            services.GetRequiredService<TimeProvider>());
        Assert.Equal(1, await dispatcher.ExecuteAsync(1, CancellationToken.None));

        var second = await queries.GetAsync(
            StaffActor(), receiptId, null, assetId, CancellationToken.None);
        Assert.Equal(first.Count, second.Count);
        Assert.Equal(
            first.Select(row => row.Id).OrderBy(id => id),
            second.Select(row => row.Id).OrderBy(id => id));

        // The first intake recorded the reading and the second re-read the same
        // bytes and left it standing. Neither failed, and neither stayed
        // silent: a swallowed failure — or a pass that never reached the reader
        // at all — would leave the same rows behind, and both are exactly what
        // this now rules out.
        var recorded = outcomes
            .Where(entry => entry.Receipt == receiptId)
            .Select(entry => entry.Outcome)
            .ToList();
        Assert.Equal(2, recorded.Count);
        Assert.Contains("recorded", recorded);
        Assert.Contains("recorded_reading_stands", recorded);
        Assert.DoesNotContain("not_recorded", recorded);
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
    /// </summary>
    private static WebApplicationFactory<Program> WithSourceCandidates(
        IntakeWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<EfRetainedInstructionAnalysisStore>();
            services.AddScoped<IRetainedInstructionAnalysisStore>(provider =>
                provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
            services.AddScoped<ISourceCandidateQueries>(provider =>
                provider.GetRequiredService<EfRetainedInstructionAnalysisStore>());
            services.AddScoped<IGetLatestRetainedInstructionAnalysis,
                GetLatestRetainedInstructionAnalysis>();
            services.AddScoped<InstructionExtractionPolicySelector>();
            services.AddScoped<IReadLogicalDocumentVersion, UnopenedDocumentReader>();
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

    /// <summary>
    /// A document reader that refuses to open anything. The Received screen
    /// needs the analysis command composed before it will show a recorded
    /// analysis, but reading this receipt's candidates never opens a document:
    /// they were recorded at retention. A reader that returned bytes here
    /// would be pretending to do work this test does not exercise.
    /// </summary>
    private sealed class UnopenedDocumentReader : IReadLogicalDocumentVersion
    {
        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException(
                "This test reads candidates recorded at retention and opens no document.");
    }
}
