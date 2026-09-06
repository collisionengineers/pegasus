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
        // to the rules is distinguishable from this reading.
        Assert.All(
            candidates,
            row => Assert.Contains(
                row.PolicyVersion,
                new[]
                {
                    ThirdPartyReportProfiles.ProfileVersion,
                    ThirdPartyReportExtraction.ProfileVersion
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

        // The panel is composed, so the screen is showing candidates rather
        // than the "not available in this environment" notice.
        Assert.DoesNotContain(
            "The retained-instruction analysis is not available",
            html,
            StringComparison.Ordinal);
    }

    [ReferencePackFact]
    public async Task ReprocessingTheSameRetainedBytesDoesNotWriteASecondSetOfCandidates()
    {
        var (bytes, _) = ReadOriginal(ReportName);
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

        // Re-evaluate the retained source. The operation key is derived from
        // the asset, so the recorded reading replays instead of being written
        // twice or overwritten.
        await services.GetRequiredService<IReevaluateIntake>().ExecuteAsync(
            new(
                receiptId,
                receipt!.Version,
                StaffActor(),
                $"reevaluate:{receiptId:N}",
                "Re-reading the retained third-party report."));

        var second = await queries.GetAsync(
            StaffActor(), receiptId, null, assetId, CancellationToken.None);
        Assert.Equal(first.Count, second.Count);
        Assert.Equal(
            first.Select(row => row.Id).OrderBy(id => id),
            second.Select(row => row.Id).OrderBy(id => id));
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
