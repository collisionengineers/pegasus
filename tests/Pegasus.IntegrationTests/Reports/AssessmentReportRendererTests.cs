using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Reports;
using UglyToad.PdfPig;

namespace Pegasus.IntegrationTests.Reports;

public sealed class AssessmentReportRendererTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [Trait("Category", "Browser")]
    [InlineData(AssessmentReportOutcome.TotalLoss, "equitable settlement")]
    [InlineData(AssessmentReportOutcome.Repairable, "repairable proposition")]
    [InlineData(AssessmentReportOutcome.CashInLieu, "cash in lieu")]
    [InlineData(AssessmentReportOutcome.ContractRepair, "contract repair")]
    public async Task ApplicationCompositionRendersApprovedOutcomeWithRepresentativeContent(AssessmentReportOutcome outcome, string outcomeText)
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();

        var generate = scope.ServiceProvider.GetRequiredService<GenerateAssessmentReportDraft>();
        var report = await generate.ExecuteAsync(
            Snapshot(outcome), CaseReportArtifactKind.AssessmentReport);
        var feeNote = await generate.ExecuteAsync(
            Snapshot(outcome), CaseReportArtifactKind.FeeNote);
        AssertArtifact(report);
        AssertArtifact(feeNote);

        var assessmentText = PdfText(report.Pdf);
        Assert.Contains(outcome switch
        {
            AssessmentReportOutcome.TotalLoss => "TOTAL LOSS REPORT",
            AssessmentReportOutcome.Repairable => "REPAIRABLE REPORT",
            AssessmentReportOutcome.CashInLieu => "CASH IN LIEU REPORT",
            AssessmentReportOutcome.ContractRepair => "CONTRACT REPAIR REPORT",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        }, assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vehicle Images", assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Statement of Truth", assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Front bumper", assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Right rear", assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quarter panel", assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VIN Checked", assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Storage Per Day", assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Salvage Agent Reference", assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(outcomeText, assessmentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Ed Mawdsley — ATA VDA AQP",
            assessmentText,
            StringComparison.OrdinalIgnoreCase);

        var feeText = PdfText(feeNote.Pdf);
        Assert.Contains("FEE NOTE", feeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Subtotal (Net)", feeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VAT @ 20%", feeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TOTAL DUE", feeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lloyds Bank", feeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("30-12-80", feeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("50858868", feeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(AssessmentReportContract.VatNumber, feeText, StringComparison.OrdinalIgnoreCase);

        var evidence = Environment.GetEnvironmentVariable("PEGASUS_RENDER_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            await File.WriteAllBytesAsync(Path.Combine(evidence, $"{outcome}-{report.SuggestedFileName}"), report.Pdf);
            await File.WriteAllBytesAsync(Path.Combine(evidence, $"{outcome}-{feeNote.SuggestedFileName}"), feeNote.Pdf);
        }
    }

    [Fact]
    [Trait("Category", "Browser")]
    public async Task NormalDensityFlowsLongListsAndMultiplePhotosAcrossPagesWithoutClipping()
    {
        const string reference = "CE-STRESS-DENSITY";
        var image = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "reference", "eva_information", "screenshots", "engineer-screens", "engineer1.png"));
        var hash = Convert.ToHexStringLower(SHA256.HashData(image));
        var snapshot = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            OurReference = reference,
            NewParts = Enumerable.Range(1, 80).Select(index => $"Stress new part {index:D3}").ToArray(),
            Repairs = Enumerable.Range(1, 80).Select(index => $"Stress repair {index:D3}").ToArray(),
            Operations = Enumerable.Range(1, 80).Select(index => $"Stress operation {index:D3}").ToArray(),
            Photos = Enumerable.Range(1, 8)
                .Select(index => new ReportImageEvidence($"stress-photo-{index:D2}", "image/png", image, hash))
                .ToArray(),
        };

        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<GenerateAssessmentReportDraft>()
            .ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);

        using var document = PdfDocument.Open(result.Pdf);
        var pages = document.GetPages().ToArray();
        var text = string.Join(Environment.NewLine, pages.Select(page => page.Text));

        Assert.True(pages.Length >= 8, $"Expected normal-density stress content to flow across pages; rendered {pages.Length}.");
        Assert.All(pages, page => Assert.Contains(reference, page.Text, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Stress new part 080", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stress repair 080", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stress operation 080", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Statement of Truth", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ed Mawdsley", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.DoesNotContain('«', text);
        Assert.True(pages.Sum(page => page.GetImages().Count()) >= 8, "Every accepted stress photo must remain embedded in the flowed PDF.");
    }

    [Fact]
    public void NoSignatoryResourceIsEmbedded()
    {
        var assembly = typeof(PlaywrightAssessmentReportRenderer).Assembly;
        Assert.DoesNotContain(
            assembly.GetManifestResourceNames(),
            name => name.Contains("brand.signatures", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Browser")]
    public async Task MissingQualificationsRenderTheSignatoryNameAlone()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();
        var ready = Snapshot(AssessmentReportOutcome.Repairable);
        var snapshot = ready with
        {
            Signatory = new ReportSignatory(
                "Neil O'Reilly",
                null,
                ready.Signatory.SignatureContent,
                "image/png"),
        };

        var result = await scope.ServiceProvider
            .GetRequiredService<GenerateAssessmentReportDraft>()
            .ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);
        var text = PdfText(result.Pdf);

        Assert.Contains("Neil O'Reilly", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Neil O'Reilly —", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "Browser")]
    // Each marker is unique to one template: the assessment report's own
    // approved covering sentence mentions a fee note, so the title alone
    // does not discriminate.
    [InlineData(CaseReportArtifactKind.AssessmentReport, "REPAIRABLE REPORT", "TOTAL DUE")]
    [InlineData(CaseReportArtifactKind.FeeNote, "TOTAL DUE", "REPAIRABLE REPORT")]
    public async Task OnlyTheRequestedKindIsRendered(
        CaseReportArtifactKind kind, string present, string absent)
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();

        var artifact = await scope.ServiceProvider
            .GetRequiredService<GenerateAssessmentReportDraft>()
            .ExecuteAsync(Snapshot(AssessmentReportOutcome.Repairable), kind);

        AssertArtifact(artifact);
        var text = PdfText(artifact.Pdf);
        Assert.Contains(present, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(absent, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Browser")]
    public async Task TheGuideSentencePrintsOnlyWhenDisclosedAndGlassesWasUsed()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();
        var generate = scope.ServiceProvider.GetRequiredService<GenerateAssessmentReportDraft>();
        var ready = Snapshot(AssessmentReportOutcome.Repairable);

        var disclosed = await generate.ExecuteAsync(
            ready with
            {
                Content = new CaseReportContentSwitches(true, false, false),
                Guides = new ReportGuideSources([ValuationSource.Glasses]),
            },
            CaseReportArtifactKind.AssessmentReport);
        var otherGuide = await generate.ExecuteAsync(
            ready with
            {
                Content = new CaseReportContentSwitches(true, false, false),
                Guides = new ReportGuideSources([ValuationSource.Cazana]),
            },
            CaseReportArtifactKind.AssessmentReport);

        Assert.Contains("Glass", PdfText(disclosed.Pdf), StringComparison.Ordinal);
        Assert.DoesNotContain("Glass", PdfText(otherGuide.Pdf), StringComparison.Ordinal);
        // The unrelated legal statements are untouched either way.
        Assert.Contains("Statement of Truth", PdfText(otherGuide.Pdf), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Parts prices are subject to fluctuation", PdfText(otherGuide.Pdf), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Browser")]
    public async Task TheCanonicalBreakdownPrintsTheEstimatesOwnVatPercentage()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();
        var ready = Snapshot(AssessmentReportOutcome.Repairable);

        var artifact = await scope.ServiceProvider
            .GetRequiredService<GenerateAssessmentReportDraft>()
            .ExecuteAsync(ready, CaseReportArtifactKind.AssessmentReport);
        var text = PdfText(artifact.Pdf);

        Assert.Contains("Panel Labour", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Paint Labour", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Paint Materials", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ready.Costs.VatLabel, text, StringComparison.Ordinal);
        Assert.DoesNotContain("parts & paint only", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Browser")]
    public async Task CropAndRotationReachTheRenderedDocument()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();
        var ready = Snapshot(AssessmentReportOutcome.Repairable);
        var photo = ready.Photos.Single();
        var snapshot = ready with
        {
            Photos =
            [
                photo with
                {
                    CustodyReference = "supporting",
                    Role = CaseAssetReportRole.Supporting,
                    Order = 1,
                    Rotation = CaseAssetRotation.Clockwise270,
                    Crop = new CaseAssetCrop(0.25m, 0.25m, 0.5m, 0.5m),
                },
                photo with
                {
                    CustodyReference = "close-up",
                    Role = CaseAssetReportRole.CloseUp,
                    Rotation = CaseAssetRotation.Clockwise90,
                },
                photo with { CustodyReference = "overview", Role = CaseAssetReportRole.Overview },
            ],
        };

        var artifact = await scope.ServiceProvider
            .GetRequiredService<GenerateAssessmentReportDraft>()
            .ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);

        using var document = PdfDocument.Open(artifact.Pdf);
        Assert.True(
            document.GetPages().Sum(page => page.GetImages().Count()) >= 3,
            "Every prepared image must remain embedded after rotation and cropping.");
        Assert.Equal(
            ["close-up", "overview", "supporting"],
            snapshot.OrderedPhotos.Select(item => item.CustodyReference));
    }

    [Fact]
    [Trait("Category", "Browser")]
    public async Task AnOversizedImageIsRefusedByTheAdapterNamingTheImage()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();
        var oversized = new byte[AssessmentReportRenderPolicy.MaximumImageBytes + 1];
        var snapshot = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            Photos =
            [
                new ReportImageEvidence(
                    "box://case/oversized.png", "image/png", oversized,
                    Convert.ToHexStringLower(SHA256.HashData(oversized))),
            ],
        };

        var exception = await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => scope.ServiceProvider
                .GetRequiredService<IAssessmentReportRenderer>()
                .RenderAsync(snapshot, CaseReportArtifactKind.AssessmentReport));

        Assert.Contains("box://case/oversized.png", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Browser")]
    public async Task AnAlreadyCancelledRenderIsAbandonedRatherThanCompleted()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scope.ServiceProvider
                .GetRequiredService<IAssessmentReportRenderer>()
                .RenderAsync(
                    Snapshot(AssessmentReportOutcome.Repairable),
                    CaseReportArtifactKind.AssessmentReport,
                    cancelled.Token));
    }

    [Fact]
    public async Task TheRendererPublishesItsEngineVersionWithoutRendering()
    {
        await using var provider = RendererProvider();
        await using var scope = provider.CreateAsyncScope();

        var engineVersion = scope.ServiceProvider
            .GetRequiredService<IAssessmentReportRenderer>().EngineVersion;

        Assert.Contains("Playwright", engineVersion, StringComparison.Ordinal);
        Assert.Contains("Chromium", engineVersion, StringComparison.Ordinal);
    }

    private static void AssertArtifact(RenderedReportArtifact artifact)
    {
        Assert.True(artifact.Pdf.AsSpan().StartsWith("%PDF"u8));
        Assert.True(artifact.PageCount >= 1);
        Assert.Equal(64, artifact.Sha256.Length);
        Assert.Equal(AssessmentReportContract.TemplateVersion, artifact.TemplateVersion);
        Assert.Contains("Playwright", artifact.EngineVersion, StringComparison.Ordinal);
    }

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure((_, options) =>
            options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=renderer;Trusted_Connection=True"));
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }

    private static string PdfText(byte[] bytes)
    {
        using var document = PdfDocument.Open(bytes);
        return string.Join(Environment.NewLine, document.GetPages().Select(page => page.Text));
    }

    private static AssessmentReportSnapshot Snapshot(AssessmentReportOutcome outcome)
    {
        var image = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "reference", "eva_information", "screenshots", "engineer-screens", "engineer1.png"));
        return new(
            OurReference: $"CE-{outcome}", YourReference: "P-100", ReportDate: new DateOnly(2026, 8, 19), ClaimantName: "Alex Example", IncidentDate: new DateOnly(2026, 8, 1),
            InstructionsReceived: new DateOnly(2026, 8, 2), Assessed: new DateOnly(2026, 8, 3), ReportFor: ["Approved Principal", "1 Example Street"],
            Vehicle: new ReportVehicle("PK12 TMZ", "Ford", "Focus", "2012", "car", "good", "80,000 miles", "online_data", "VIN", "1600 cc", "Petrol", true, "manual", "Blue", "Hatchback", new(2027, 1, 2), new(2027, 3, 4), "None", "P0001", true, "Secure bumper", 25m),
            Outcome: outcome, LegalStatus: "roadworthy", UnroadworthyReason: null, ImpactSeverity: "moderate", ImpactLocation: "right_rear", AssessmentMethod: "image_based", LocationAddress: null,
            EngineerValue: 5_000m, RetailValue: 5_000m, TradeValue: 4_000m, SalvageCategory: outcome == AssessmentReportOutcome.TotalLoss ? "S" : null, SalvageValue: outcome == AssessmentReportOutcome.TotalLoss ? 500m : null,
            Costs: Costs(), NewParts: ["Front bumper"], Repairs: ["Bonnet"], Operations: ["Paint front panels"],
            Damage: new ReportDamage([new("Right rear", "Moderate", "Quarter panel")], "ok", "worn", "damaged", "illegal", "ok", "locked", "deployed", "not_fitted", "repair_kit", "not_fitted", "Door scratch", 75m, "Red paint"),
            Settlement: new ReportSettlement(250m, 100m, true, 6_000m, 4_125m, 4, "Parts delay", "None", 20m, 80m, new(2026, 8, 4), 35m, 200m, "Repairer", "Salvage Co", "SAL-1", true, false, true, new(2026, 8, 20)),
            HistoryCheck: "History clear", EngineerComments: "No further comments.", Signatory: new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", image, "image/png"), AgreedFee: 120m, FeeDescriptionLines: ["Engineering assessment"],
            Photos: [new ReportImageEvidence("reference/eva_information/screenshots/engineer-screens/engineer1.png", "image/png", image, Convert.ToHexStringLower(SHA256.HashData(image)))],
            Sources: [new AcceptedReportSource("assessment", "7", new string('a', 64))],
            Content: CaseReportContentSwitches.None,
            Guides: ReportGuideSources.None);
    }

    /// <summary>
    /// The Current estimate the rendered fixtures price from: 50 parts, five
    /// panel hours at 30, 20 materials and 5 specialist, at 20 per cent VAT.
    /// </summary>
    private static ReportRepairCosts Costs() => ReportRepairCosts.For(
        new RepairSpecificationVersion(
            Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Accepted,
            new(RepairSpecificationSourceRoute.Manual, null, null, null),
            [
                Line(1, "repair", "Nearside door", 5m, null),
                Line(2, "new_part", "Door skin", null, 50m),
            ],
            null, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc, null, null,
            new EstimateDetails("Repairer", null, 30m, 20m, 5m, 20m, null), IsCurrent: true));

    private static CaseEstimateLineRecord Line(
        int position, string type, string description, decimal? workUnits, decimal? price) => new(
            Guid.NewGuid(), position, type, null, description, workUnits, price, false, null, null,
            "confirmed", "case", "Test evidence",
            ActorKind.Staff, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc, Quantity: 1);

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Pegasus.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
