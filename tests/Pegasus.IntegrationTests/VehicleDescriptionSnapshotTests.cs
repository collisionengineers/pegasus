using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class VehicleDescriptionSnapshotTests
{
    [ReferencePackFact]
    [Trait("Category", "Corpus")]
    public async Task GenuineQdosVehicleDescriptionRemainsSourceOnlyThroughTheSnapshot()
    {
        const string relativePath = "incident/qdos-vehicle-description.eml";
        const string sha256 = "F09402BB68E1F986B9E22F94C660C478349B6BBE12FB8958ED8E903AEDD942AA";
        var path = Path.Combine(
            Top15InstructionCorpusTests.PackRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(bytes)));

        var read = await new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System)
            .ReadAsync(
                new(
                    Path.GetFileName(path),
                    Top15InstructionCorpusTests.MediaType(path),
                    bytes,
                    DateTimeOffset.UtcNow,
                    "reference-evidence",
                    new(IntakeSourceChannel.ManualUpload, "qdos-vehicle-description")),
                CancellationToken.None);
        Assert.Equal(IntakeSourceReadStatus.Readable, read.Status);
        Assert.False(read.IsIncomplete);

        var policy = new QdosInstructionExtractionPolicy();
        var selection = new InstructionExtractionPolicySelector([policy])
            .Select(read, InstructionDocumentSignature.InstructionRole);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Same(policy, selection.Policy);

        var extracted = policy.Extract(
            read with { Content = selection.InstructionContent },
            DateTimeOffset.UtcNow,
            new(
                QdosInstructionExtractionPolicy.SupportedPrincipalCode,
                PrincipalMailRoutePolicy.Key,
                PrincipalMailRoutePolicy.Version));
        var draft = Assert.IsType<InstructionDraft>(extracted.InstructionDraft);
        var description = Assert.Single(
            extracted.Fields,
            field => field.Name == "Vehicle description");
        var candidate = Assert.Single(description.Candidates, item =>
            string.Equals(item.Value, description.SuggestedValue, StringComparison.Ordinal)
            && item.Source == IntakeEvidenceSource.PdfContent);
        Assert.False(description.HasConflict);
        Assert.Equal("SEAT LEON SPORT TDI 105", description.SuggestedValue);
        Assert.Equal("SEAT LEON SPORT TDI 105", candidate.Value);
        Assert.Equal(IntakeEvidenceSource.PdfContent, candidate.Source);
        Assert.Contains("attachment 6", candidate.SourceLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, candidate.Locator?.Page);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Null(draft.VehicleMileage);

        var snapshot = CreateSnapshot(description);
        var fact = Assert.Single(snapshot.Fields);
        Assert.Equal(CaseDataFieldNames.VehicleDescription, fact.FieldName);
        Assert.Equal(CaseDataCodes.Fact, fact.ValueKind);
        Assert.Equal(description.SuggestedValue, fact.Value);
        Assert.Equal(CaseDataCodes.IntakeEvidence, fact.SourceKind);
        Assert.Equal($"PdfContent:{candidate.SourceLabel}", fact.SourceLabel);
    }

    [Fact]
    public void ExtractedVehicleDescriptionIsRecordedAsASourceFactWithoutTypedVehicleValues()
    {
        var snapshot = CreateSnapshot(
            new(
                "Vehicle description",
                "SEAT LEON SPORT TDI 105",
                [new(
                    "SEAT LEON SPORT TDI 105",
                    IntakeEvidenceSource.PdfContent,
                    "attachment-6 page-1")],
                IsDefaulted: false,
                HasConflict: false));

        var description = Assert.Single(snapshot.Fields);
        Assert.Equal(CaseDataFieldNames.VehicleDescription, description.FieldName);
        Assert.Equal(CaseDataCodes.Fact, description.ValueKind);
        Assert.Equal(CaseDataCodes.Text, description.ValueType);
        Assert.Equal("SEAT LEON SPORT TDI 105", description.Value);
        Assert.Equal(CaseDataCodes.IntakeEvidence, description.SourceKind);
        Assert.Equal("PdfContent:attachment-6 page-1", description.SourceLabel);
        Assert.DoesNotContain(snapshot.Fields, field => field.FieldName is
            CaseDataFieldNames.VehicleRegistration or
            CaseDataFieldNames.VehicleMake or
            CaseDataFieldNames.VehicleModel);
    }

    [Fact]
    public void ConflictedVehicleDescriptionIsRefusedRatherThanRecorded()
    {
        Assert.Throws<InvalidDataException>(() => CreateSnapshot(
            new(
                "Vehicle description",
                "SEAT LEON SPORT TDI 105",
                [new(
                    "SEAT LEON SPORT TDI 105",
                    IntakeEvidenceSource.PdfContent,
                    "attachment-6 page-1")],
                IsDefaulted: false,
                HasConflict: true)));
    }

    private static CaseDataSnapshotEntity CreateSnapshot(InstructionReviewField field)
    {
        var receiptId = Guid.NewGuid();
        var receipt = new IntakeReceiptEntity
        {
            Id = receiptId,
            SourceFileName = "vehicle-description-probe",
            MediaType = "application/pdf",
            SourceHash = "source-hash",
            SourceChannel = "manual_upload",
            ExternalReceiptToken = "vehicle-description-probe",
            SourceReaderKey = "structural-probe",
            SourceReaderVersion = "1",
            ExtractionPolicyKey = QdosInstructionExtractionPolicy.Key,
            ExtractionPolicyVersion = QdosInstructionExtractionPolicy.Version,
            Decision = "case_created",
            DecisionReason = "vehicle-description-probe",
            EvidenceJson = "{\"version\":1,\"data\":[]}",
            OcrCandidatesJson = "{\"version\":1,\"data\":[]}",
            FieldsJson = EfIntakeReceiptStore.SerializeFields([field]),
            InstructionDraft = new() { SuggestedPrincipalCode = "QDOS" }
        };
        var accepted = new CaseEntity
        {
            Id = Guid.NewGuid(),
            OriginIntakeReceiptId = receiptId,
            Reference = "vehicle-description-probe",
            Type = "inspection",
            InitialState = "not_ready",
            CustodyState = "pending"
        };

        return CaseDataSnapshotFactory.Create(
            accepted,
            receipt,
            new(
                receiptId,
                1,
                ActionActor.SystemWorker("system-worker:intake-processing"),
                "vehicle-description-probe",
                CaseType.Inspection,
                "QDOS",
                new(true, false),
                new(false, "vehicle-description-probe", 1),
                CaseInspectionMode.PhysicalAddress),
            DateTimeOffset.UtcNow);
    }
}
