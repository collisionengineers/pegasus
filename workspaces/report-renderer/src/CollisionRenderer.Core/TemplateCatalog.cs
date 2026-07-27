using CollisionRenderer.Core.Design;
using CollisionRenderer.Core.Models;

namespace CollisionRenderer.Core;

/// <summary>The set of document templates the renderer can produce.</summary>
public interface ITemplateCatalog
{
    IReadOnlyList<TemplateDescriptor> List();
    TemplateDescriptor Get(string id);
    bool TryGet(string id, out TemplateDescriptor? descriptor);

    /// <summary>The bundled starter payload for a template (for "new" / "load sample").</summary>
    string GetSampleJson(string id);
}

public sealed class TemplateCatalog : ITemplateCatalog
{
    public static readonly TemplateCatalog Default = new();

    private readonly Dictionary<string, TemplateDescriptor> _byId;

    public TemplateCatalog()
    {
        var all = new[]
        {
            new TemplateDescriptor
            {
                Id = "market-valuation-evidence",
                Name = "Market Valuation Evidence",
                Description = "Retail pre-accident value evidenced by live comparable adverts.",
                ModelType = typeof(MarketValuationEvidenceDocument),
                TemplateResource = "templates/market_valuation_evidence.scriban",
                SampleResource = "samples/market_valuation_evidence.json",
                DensityProfile = DensityFitProfile.FitToPages,
                FitTargetPages = 1,
                FileNameSuffix = "market_valuation_evidence",
            },
            new TemplateDescriptor
            {
                Id = "advert-evidence-pack",
                Name = "Advert Evidence Pack",
                Description = "Comparable advert references that accompany the valuation evidence.",
                ModelType = typeof(AdvertEvidencePackDocument),
                TemplateResource = "templates/advert_evidence_pack.scriban",
                SampleResource = "samples/advert_evidence_pack.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "advert_evidence_pack",
            },
            new TemplateDescriptor
            {
                Id = "fee-note",
                Name = "Fee Note",
                Description = "VAT fee note / invoice for completed engineering work.",
                ModelType = typeof(FeeNoteDocument),
                TemplateResource = "templates/fee_note.scriban",
                SampleResource = "samples/fee_note.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "fee_note",
            },
            new TemplateDescriptor
            {
                Id = "expert-report",
                Name = "Expert Report",
                Description = "Flexible letter-style report: total loss, addendum, diminution rebuttal, Part 35.",
                ModelType = typeof(ExpertReportDocument),
                TemplateResource = "templates/expert_report.scriban",
                SampleResource = "samples/expert_report.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "report",
            },
            new TemplateDescriptor
            {
                Id = "blank-letterhead",
                Name = "Blank Letterhead",
                Description = "A minimal Collision Engineers letterhead with a free-text body.",
                ModelType = typeof(ExpertReportDocument),
                TemplateResource = "templates/expert_report.scriban",
                SampleResource = "samples/blank_letterhead.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "letterhead",
            },
            new TemplateDescriptor
            {
                Id = "repairable-contract-repair-report",
                Name = "Repairable / Contract Repair Report",
                Description = "Independent accident damage report for a repairable outcome.",
                ModelType = typeof(ExpertReportDocument),
                TemplateResource = "templates/expert_report.scriban",
                SampleResource = "samples/repairable_contract_repair_report.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "repairable_contract_repair_report",
            },
            new TemplateDescriptor
            {
                Id = "total-loss-report",
                Name = "Total Loss Report",
                Description = "Damage report where the vehicle is a write-off and settlement is engineer value less salvage.",
                ModelType = typeof(ExpertReportDocument),
                TemplateResource = "templates/expert_report.scriban",
                SampleResource = "samples/total_loss_report.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "total_loss_report",
            },
            new TemplateDescriptor
            {
                Id = "addendum-report",
                Name = "Addendum Report",
                Description = "Further commentary defending or clarifying a prior report.",
                ModelType = typeof(ExpertReportDocument),
                TemplateResource = "templates/expert_report.scriban",
                SampleResource = "samples/addendum_report.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "addendum_report",
            },
            new TemplateDescriptor
            {
                Id = "diminution-rebuttal",
                Name = "Diminution Rebuttal",
                Description = "Letter-style rebuttal of a third-party diminution-in-value claim.",
                ModelType = typeof(ExpertReportDocument),
                TemplateResource = "templates/expert_report.scriban",
                SampleResource = "samples/diminution_rebuttal.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "diminution_rebuttal",
            },
            new TemplateDescriptor
            {
                Id = "roadworthy-criminal-report",
                Name = "Roadworthy / Criminal Report",
                Description = "Safety, compliance or criminal-matter report with defect findings.",
                ModelType = typeof(ExpertReportDocument),
                TemplateResource = "templates/expert_report.scriban",
                SampleResource = "samples/roadworthy_criminal_report.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "roadworthy_criminal_report",
            },
            new TemplateDescriptor
            {
                Id = "part-35-response",
                Name = "Part 35 Responses",
                Description = "Written answers to a Schedule of Questions to the Engineer.",
                ModelType = typeof(ExpertReportDocument),
                TemplateResource = "templates/expert_report.scriban",
                SampleResource = "samples/part_35_response.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "part_35_response",
            },
            new TemplateDescriptor
            {
                Id = "response-letter",
                Name = "Response Letter",
                Description = "Letter-style dispute or correspondence response.",
                ModelType = typeof(ExpertReportDocument),
                TemplateResource = "templates/expert_report.scriban",
                SampleResource = "samples/response_letter.json",
                DensityProfile = DensityFitProfile.None,
                FileNameSuffix = "response_letter",
            },
        };

        _byId = all.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<TemplateDescriptor> List() => _byId.Values
        .OrderBy(d => d.Name, StringComparer.Ordinal).ToList();

    public TemplateDescriptor Get(string id) =>
        _byId.TryGetValue(id, out var d)
            ? d
            : throw new KeyNotFoundException(
                $"Unknown template '{id}'. Known: {string.Join(", ", _byId.Keys)}");

    public bool TryGet(string id, out TemplateDescriptor? descriptor)
    {
        var found = _byId.TryGetValue(id, out var d);
        descriptor = d;
        return found;
    }

    public string GetSampleJson(string id) => EmbeddedResources.ReadText(Get(id).SampleResource);
}
