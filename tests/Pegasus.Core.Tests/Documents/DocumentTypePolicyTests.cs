using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Documents;

/// <summary>
/// A document's type changed after filing (operator, 22 October 2026; R204):
/// every type but Original source, a typed reason of up to 500 characters, and
/// Audit report as Mark as original report's act.
/// </summary>
public sealed class DocumentTypePolicyTests
{
    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public void EveryTypeButOriginalSourceMayBeChosen()
    {
        DocumentSemanticRole[] choosable =
        [
            DocumentSemanticRole.Instruction,
            DocumentSemanticRole.Image,
            DocumentSemanticRole.Correspondence,
            DocumentSemanticRole.EngineerReport,
            DocumentSemanticRole.AuditReport,
            DocumentSemanticRole.Estimate,
            DocumentSemanticRole.ValuationEvidence,
            DocumentSemanticRole.Other
        ];

        Assert.Equal(choosable, DocumentTypePolicy.StaffMayChoose);
    }

    /// <summary>
    /// The Upload page offers a declared-Case file every staff type but Image,
    /// which the file's format decides, and Audit report, which is Mark as
    /// original report's after filing; Other is the choice until changed
    /// (operator, 30 October 2026; R553, R554).
    /// </summary>
    [Fact]
    public void AnUploadOffersEveryStaffTypeButImageAndAuditReport()
    {
        DocumentSemanticRole[] offered =
        [
            DocumentSemanticRole.Instruction,
            DocumentSemanticRole.Correspondence,
            DocumentSemanticRole.EngineerReport,
            DocumentSemanticRole.Estimate,
            DocumentSemanticRole.ValuationEvidence,
            DocumentSemanticRole.Other
        ];

        Assert.Equal(offered, DocumentTypePolicy.UploadChoices);
        Assert.Equal(DocumentSemanticRole.Other, DocumentTypePolicy.DefaultUploadType);
        Assert.Contains(DocumentTypePolicy.DefaultUploadType, DocumentTypePolicy.UploadChoices);
    }

    [Theory]
    [InlineData("estimate.pdf", DocumentSemanticRole.Other, "application/pdf", false,
        "Instruction,Correspondence,EngineerReport,Estimate,ValuationEvidence")]
    [InlineData("instruction.pdf", DocumentSemanticRole.Instruction, "application/pdf", true,
        "Correspondence,EngineerReport,AuditReport,Estimate,ValuationEvidence,Other")]
    [InlineData("photo.png", DocumentSemanticRole.Other, "image/png", false,
        "Instruction,Image,Correspondence,EngineerReport,Estimate,ValuationEvidence")]
    [InlineData("front.jpg", DocumentSemanticRole.Image, "image/jpeg", true,
        "Instruction,Correspondence,EngineerReport,Estimate,ValuationEvidence,Other")]
    [InlineData("original-report.pdf", DocumentSemanticRole.AuditReport, "application/pdf", false,
        "Instruction,Correspondence,EngineerReport,Estimate,ValuationEvidence,Other")]
    public void ARowOffersEveryChoosableTypeItMayTake(
        string fileName,
        DocumentSemanticRole current,
        string mediaType,
        bool originalReportMissing,
        string expected)
    {
        Assert.Equal(
            expected.Split(','),
            DocumentTypePolicy.Choices(File(current, mediaType, fileName), originalReportMissing)
                .Select(role => role.ToString()));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void AVersionThatIsNotTheLiveOneOffersNoChoice(bool isCurrent, bool isRemoved)
    {
        var file = File(DocumentSemanticRole.Other, "application/pdf", "superseded.pdf");

        Assert.Empty(DocumentTypePolicy.Choices(
            file with { Version = file.Version with { IsCurrent = isCurrent, IsLogicallyRemoved = isRemoved } },
            originalReportMissing: true));
    }

    [Theory]
    [InlineData("principal actor", typeof(StaffAuthorizationException))]
    [InlineData("empty case", typeof(ArgumentException))]
    [InlineData("empty occurrence", typeof(ArgumentException))]
    [InlineData("empty version", typeof(ArgumentException))]
    [InlineData("original source", typeof(ArgumentOutOfRangeException))]
    [InlineData("undefined type", typeof(ArgumentOutOfRangeException))]
    [InlineData("negative version", typeof(ArgumentOutOfRangeException))]
    [InlineData("blank key", typeof(ArgumentException))]
    [InlineData("long key", typeof(ArgumentOutOfRangeException))]
    [InlineData("blank lease", typeof(ArgumentException))]
    [InlineData("blank reason", typeof(ArgumentException))]
    [InlineData("501-character reason", typeof(ArgumentOutOfRangeException))]
    public void AnIncompleteRequestIsRefused(string row, Type refusal)
    {
        var request = Request();
        var refused = row switch
        {
            "principal actor" => request with { Actor = ActionActor.Principal(Guid.NewGuid()) },
            "empty case" => request with { CaseId = Guid.Empty },
            "empty occurrence" => request with { OccurrenceId = Guid.Empty },
            "empty version" => request with { VersionId = Guid.Empty },
            "original source" => request with { NewRole = DocumentSemanticRole.OriginalSource },
            "undefined type" => request with { NewRole = (DocumentSemanticRole)99 },
            "negative version" => request with { ExpectedCaseVersion = -1 },
            "blank key" => request with { OperationKey = " " },
            "long key" => request with { OperationKey = new string('k', 101) },
            "blank lease" => request with { EditLeaseToken = " " },
            "blank reason" => request with { Reason = " " },
            "501-character reason" => request with { Reason = new string('r', 501) },
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };

        Assert.IsType(refusal, Record.Exception(() => DocumentTypePolicy.Validate(refused)));
    }

    [Fact]
    public void AReasonOfFiveHundredCharactersIsKeptTrimmed()
    {
        Assert.Null(Record.Exception(() =>
            DocumentTypePolicy.Validate(Request() with { Reason = new string('r', 500) })));
        Assert.Equal("Filed as the wrong type.", DocumentTypePolicy.RequireReason("  Filed as the wrong type.  "));
    }

    [Theory]
    [InlineData(DocumentSemanticRole.Other, DocumentSemanticRole.Other, "application/pdf")]
    [InlineData(DocumentSemanticRole.Image, DocumentSemanticRole.Other, "application/pdf")]
    public void AChangeToItsOwnTypeOrToImageForAFileThatIsNotAJpegOrPngIsRefused(
        DocumentSemanticRole next,
        DocumentSemanticRole current,
        string mediaType)
    {
        Assert.Throws<InvalidOperationException>(() => DocumentTypePolicy.RequireChange(current, next, mediaType));
    }

    [Theory]
    [InlineData(DocumentSemanticRole.Other, DocumentSemanticRole.Image, "image/png")]
    [InlineData(DocumentSemanticRole.Image, DocumentSemanticRole.Other, "image/jpeg")]
    public void AJpegOrPngMayBecomeAnImageOrStopBeingOne(
        DocumentSemanticRole current,
        DocumentSemanticRole next,
        string mediaType)
    {
        Assert.Null(Record.Exception(() => DocumentTypePolicy.RequireChange(current, next, mediaType)));
    }

    [Fact]
    public async Task OnlyAChangeToAuditReportReadsTheDocumentFirst()
    {
        var store = new RecordingStore();
        var reader = new RecordingReader(Reading);
        var sut = new SetDocumentType(store, reader);
        var toEstimate = Request();
        var toAuditReport = Request() with { NewRole = DocumentSemanticRole.AuditReport };

        await sut.ExecuteAsync(toEstimate);
        await sut.ExecuteAsync(toAuditReport);

        Assert.Collection(
            store.Calls,
            call => Assert.Equal((toEstimate, (OriginalReportReading?)null), call),
            call => Assert.Equal((toAuditReport, (OriginalReportReading?)Reading), call));
        Assert.Equal(
            (toAuditReport.Actor, toAuditReport.CaseId, toAuditReport.OccurrenceId, toAuditReport.VersionId),
            Assert.Single(reader.Reads));
    }

    [Fact]
    public async Task ARefusedRequestReadsAndRecordsNothing()
    {
        var store = new RecordingStore();
        var reader = new RecordingReader(Reading);
        var sut = new SetDocumentType(store, reader);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.ExecuteAsync(
            Request() with { NewRole = DocumentSemanticRole.AuditReport, Reason = "" }));

        Assert.Empty(store.Calls);
        Assert.Empty(reader.Reads);
    }

    private static readonly OriginalReportReading Reading =
        new(new string('a', 64), "Laird Assessors", "2026-09-01", "roadworthy", "repairable", false);

    private static SetDocumentTypeCommand Request() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        DocumentSemanticRole.Estimate,
        Staff,
        "Filed as the wrong type.",
        "set-document-type",
        4,
        "edit-lease-token");

    private static CaseFile File(DocumentSemanticRole role, string mediaType, string fileName)
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var recordedAt = new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);
        return new(
            new DocumentOccurrence(
                Guid.NewGuid(), Guid.NewGuid(), documentId, versionId, role, DocumentSource.Intake,
                "intake:file", recordedAt, []),
            new DocumentVersion(
                versionId, documentId, 1, fileName, mediaType, 10, new string('a', 64),
                DocumentCustodyStatus.Confirmed, recordedAt, "Staff:test", IsCurrent: true,
                IsLogicallyRemoved: false, RemovalReason: null));
    }

    private sealed class RecordingReader(OriginalReportReading? reading) : IReadOriginalReport
    {
        public List<(ActionActor, Guid, Guid, Guid)> Reads { get; } = [];

        public Task<OriginalReportReading?> ForIntakeAsync(
            Guid receiptId, Guid standaloneAuditEvidenceId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OriginalReportReading?> ForDocumentAsync(
            ActionActor actor, Guid caseId, Guid occurrenceId, Guid versionId, CancellationToken cancellationToken)
        {
            Reads.Add((actor, caseId, occurrenceId, versionId));
            return Task.FromResult(reading);
        }

        public Task<OriginalReportRecognition> RecogniseFiledAssetAsync(
            Guid receiptId, IntakeAssetRecord asset, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingStore : ISetDocumentTypeStore
    {
        public List<(SetDocumentTypeCommand, OriginalReportReading?)> Calls { get; } = [];

        public Task SetDocumentTypeAsync(
            SetDocumentTypeCommand command,
            OriginalReportReading? reading,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((command, reading));
            return Task.CompletedTask;
        }
    }
}
