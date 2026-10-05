using System.Security.Cryptography;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

public sealed class FileGuideValuationReportTests
{
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly byte[] Pdf = "%PDF-1.7\nvaluation\n%%EOF"u8.ToArray();

    private static FileGuideValuationReport Filing(ICaseArtifactCustody custody, string? printedText) =>
        new(custody, new StubText(printedText));

    /// <summary>
    /// The report is retained as a Case artifact under the provider's record of
    /// the valuation, never under the Case form's operation key, and with no
    /// expected Case version: filing it must not refuse the Engineer's Save.
    /// </summary>
    [Fact]
    public async Task TheReportIsRetainedAsACaseArtifactUnderItsOwnKey()
    {
        var custody = new RecordingCustody();
        var caseId = Guid.NewGuid();

        await Filing(custody, "Vehicle Valuation Report Kia Sportage - KY12CAB").ExecuteAsync(
            new(Engineer, caseId, ValuationSource.Glasses, "KY12CAB", new DateOnly(2026, 10, 1), new StubReport(Pdf)),
            default);

        var retained = Assert.Single(custody.Requests);
        Assert.Equal(caseId, retained.CaseId);
        Assert.Null(retained.IntakeReceiptId);
        Assert.Equal("Glass's valuation KY12CAB 2026-10.pdf", retained.FileName);
        Assert.Equal("application/pdf", retained.MediaType);
        Assert.Equal(DocumentSemanticRole.Other, retained.SemanticRole);
        Assert.Equal(DocumentSource.Generated, retained.Source);
        Assert.Null(retained.ExpectedCaseVersion);
        Assert.Equal("guide-valuation-report:Glasses:glass-stock:33636950", retained.OperationKey);
        Assert.Equal(retained.OperationKey, retained.OccurrenceIdentity);
        Assert.Equal(Pdf.LongLength, retained.ContentLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Pdf)), retained.Sha256);
        Assert.Equal(Pdf, custody.Contents.Single());
    }

    [Fact]
    public async Task AnAnswerThatIsNotAPdfIsNeverFiled()
    {
        var custody = new RecordingCustody();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Filing(custody, "KY12CAB").ExecuteAsync(
            new(Engineer, Guid.NewGuid(), ValuationSource.Glasses, "KY12CAB", new DateOnly(2026, 10, 1),
                new StubReport("<html>login</html>"u8.ToArray())),
            default));

        Assert.Empty(custody.Requests);
    }

    /// <summary>The plate is read as printed: spacing and case in either are ignored.</summary>
    [Theory]
    [InlineData("Kia Sportage - ky12 cab", "KY12CAB")]
    [InlineData("Kia Sportage - KY12CAB", "KY12 CAB")]
    [InlineData("Kia Sportage - KY12 CAB", "KY12CAB")]
    public async Task TheReportIsFiledWhenItsTextNamesTheRegistrationInAnySpacing(string printed, string registration)
    {
        var custody = new RecordingCustody();

        await Filing(custody, printed).ExecuteAsync(
            new(Engineer, Guid.NewGuid(), ValuationSource.Glasses, registration, new DateOnly(2026, 10, 1),
                new StubReport(Pdf)),
            default);

        Assert.Single(custody.Requests);
    }

    /// <summary>
    /// The provider's shared account can hand back another vehicle's print. It
    /// is refused with a flags-only reason that never carries a plate.
    /// </summary>
    [Fact]
    public async Task AReportThatNamesAnotherRegistrationIsNeverFiled()
    {
        var custody = new RecordingCustody();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Filing(custody, "Vehicle Valuation Report Kia Sportage - MP23KTV").ExecuteAsync(
                new(Engineer, Guid.NewGuid(), ValuationSource.Glasses, "KY12CAB", new DateOnly(2026, 10, 1),
                    new StubReport(Pdf)),
                default));

        Assert.Contains("registration=different", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("KY12CAB", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("MP23KTV", refused.Message, StringComparison.Ordinal);
        Assert.Empty(custody.Requests);
    }

    /// <summary>A PDF whose text cannot be read, or has none, names no registration.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AReportWhoseTextCannotBeReadIsNeverFiled(string? printed)
    {
        var custody = new RecordingCustody();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Filing(custody, printed).ExecuteAsync(
                new(Engineer, Guid.NewGuid(), ValuationSource.Glasses, "KY12CAB", new DateOnly(2026, 10, 1),
                    new StubReport(Pdf)),
                default));

        Assert.Contains("registration=absent", refused.Message, StringComparison.Ordinal);
        Assert.Empty(custody.Requests);
    }

    /// <summary>Answers one page of the given text, or an unreadable PDF for null.</summary>
    private sealed class StubText(string? printed) : IExtractPdfPageText
    {
        public Task<PdfTextExtraction?> ExtractAsync(
            ReadOnlyMemory<byte> content, int maximumCharacters, CancellationToken cancellationToken) =>
            Task.FromResult<PdfTextExtraction?>(printed is null
                ? null
                : new PdfTextExtraction(1, [new PdfPageText(1, printed, NeedsOcr: false, Truncated: false)]));
    }

    private sealed class StubReport(byte[] content) : IGuideValuationReport
    {
        public string Identity => "glass-stock:33636950";

        public Task<byte[]> FetchPdfAsync(CancellationToken cancellationToken) => Task.FromResult(content);
    }

    private sealed class RecordingCustody : ICaseArtifactCustody
    {
        public List<CaseArtifactCustodyRequest> Requests { get; } = [];

        public List<byte[]> Contents { get; } = [];

        public async Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            using var copy = new MemoryStream();
            await request.Content.CopyToAsync(copy, cancellationToken);
            Contents.Add(copy.ToArray());
            return new(CaseArtifactCustodyDisposition.Confirmed, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "box-file", "box-version", request.Sha256, request.ContentLength, request.MediaType, null, null);
        }
    }
}
