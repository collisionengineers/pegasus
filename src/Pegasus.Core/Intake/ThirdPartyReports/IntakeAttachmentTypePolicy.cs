using System.Text.RegularExpressions;
using Pegasus.Core.Documents;

namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>
/// The type a new Case's mailed attachment is filed with when what it prints
/// says it is not an instruction (operator, 27 November 2026; R1336, R1337).
/// Read from the attachment's own retained text, never its file name:
/// <list type="bullet">
/// <item>An Audatex costing that prints no assessment number, the invoice
/// form Exclusive Repair Network sends, is an External estimate. An Audatex
/// report that prints its assessment number is left as filed, so the Worker
/// types it by its header when it reads it (R1140, R1281, R1285).</item>
/// <item>A photo sheet, a PDF whose pages carry only photographs, is Other;
/// the photographs inside it are filed as Image on their own.</item>
/// </list>
/// Anything else, and any document a report signature claims, has no type
/// here and is filed as an Instruction.
/// </summary>
public static class IntakeAttachmentTypePolicy
{
    private static readonly Regex AudatexFooter = ThirdPartyRegex.Create(@"\bAudatex\s+System\b");
    private static readonly Regex AssessmentNumber = ThirdPartyRegex.Create(@"\bAssessment\s+Number\b");

    // Laird prints its photo sheet as its own document: its web address and
    // the "Vehicle Images" heading, without any of its report titles. The
    // photo sheet signature denies Laird's address, because a Laird report
    // carries the same heading over its appended photographs.
    private static readonly Regex LairdAddress = ThirdPartyRegex.Create(@"laird-assessors\.com");
    private static readonly Regex VehicleImagesHeading = ThirdPartyRegex.Create(@"\bVehicle\s+Images\b");

    /// <summary>
    /// The type <paramref name="documentText"/> gives its attachment, or null
    /// when it is filed as an Instruction.
    /// </summary>
    public static DocumentSemanticRole? FilingType(string? documentText)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return null;
        }

        var document = ThirdPartySourcePage.Flatten(documentText);
        var signatures = ThirdPartyReportProfiles.MatchingSignatures(document);
        if (signatures.Any(signature => signature.Role is ThirdPartyDocumentRole.EngineerReport
                or ThirdPartyDocumentRole.VehicleCapture))
        {
            return null;
        }

        if (AudatexFooter.IsMatch(document) && !AssessmentNumber.IsMatch(document))
        {
            return DocumentSemanticRole.ExternalEstimate;
        }

        return signatures switch
        {
            [{ Key: ThirdPartyReportProfiles.ImageEvidenceSignatureKey }] => DocumentSemanticRole.Other,
            [] when LairdAddress.IsMatch(document) && VehicleImagesHeading.IsMatch(document) =>
                DocumentSemanticRole.Other,
            _ => null
        };
    }
}
