namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>
/// One document out of a whole-source reading. The reader labels every
/// fragment with the document it came from (<c>uploaded mail.eml, attachment
/// 2: report.pdf, page 3</c>), so a file's own pages, scanned pages and images
/// can be lifted out and read as that file alone. A report is read this way —
/// never flattened together with the e-mail body or the estimate beside it,
/// which is how two different documents in one mail used to look like one
/// ambiguous one.
/// </summary>
public static class ThirdPartyReportDocuments
{
    /// <summary>
    /// The reading of one document: its page and form-field fragments, its OCR
    /// candidates and its image candidates, and nothing from any other document
    /// in the same source. Issues and transport evidence belong to the whole
    /// source and are not carried.
    /// </summary>
    public static IntakeSourceReadResult ForDocument(IntakeSourceReadResult readResult, string documentLabel)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentLabel);
        var content = readResult.Content
            .Where(fragment => string.Equals(
                InstructionExtractionPolicySelector.DocumentIdentity(fragment.SourceLabel),
                documentLabel,
                StringComparison.Ordinal))
            .ToArray();
        var scanned = readResult.ScannedPdfPages
            .Where(candidate => string.Equals(candidate.SourceLabel, documentLabel, StringComparison.Ordinal))
            .ToArray();
        var assets = readResult.AssetCandidates
            .Where(asset => string.Equals(asset.SourceLabel, documentLabel, StringComparison.Ordinal)
                || asset.SourceLabel.StartsWith(documentLabel + ",", StringComparison.Ordinal))
            .ToArray();
        return readResult with
        {
            Content = content,
            TransportEvidence = [],
            Issues = [],
            RequiresOcr = scanned.Length > 0,
            Assets = assets,
            OcrCandidates = scanned,
            Attachments = null,
            Companions = null,
            RootEmail = null
        };
    }
}
