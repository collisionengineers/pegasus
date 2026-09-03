using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The persisted codes for <see cref="IntakeEvidenceSource"/>, in one place.
///
/// There were two copies of this map — one in <c>EfIntakeReceiptStore</c> and
/// one in <c>InspectionAddressResolutionStore</c> — and they drifted the moment
/// a member was added: receipts persisted the new source happily and the
/// address-resolution snapshot then refused to read it back, failing case
/// allocation with an unclassified fault. A vocabulary written to the database
/// gets exactly one owner.
/// </summary>
internal static class IntakeEvidenceSourceCodes
{
    public static string ToCode(IntakeEvidenceSource value) => value switch
    {
        IntakeEvidenceSource.EmailBody => "email_body",
        IntakeEvidenceSource.PdfContent => "pdf_content",
        IntakeEvidenceSource.DocumentContent => "document_content",
        IntakeEvidenceSource.ImageContent => "image_content",
        IntakeEvidenceSource.Sender => "sender",
        IntakeEvidenceSource.Subject => "subject",
        IntakeEvidenceSource.FileName => "file_name",
        IntakeEvidenceSource.MimeType => "mime_type",
        IntakeEvidenceSource.StaffCorrection => "staff_correction",
        IntakeEvidenceSource.SystemDefault => "system_default",
        IntakeEvidenceSource.ProviderDeclaration => "provider_declaration",
        _ => throw new InvalidOperationException(
            $"Unknown intake evidence source '{(int)value}'.")
    };

    public static IntakeEvidenceSource Parse(string code) => code switch
    {
        "email_body" => IntakeEvidenceSource.EmailBody,
        "pdf_content" => IntakeEvidenceSource.PdfContent,
        "document_content" => IntakeEvidenceSource.DocumentContent,
        "image_content" => IntakeEvidenceSource.ImageContent,
        "sender" => IntakeEvidenceSource.Sender,
        "subject" => IntakeEvidenceSource.Subject,
        "file_name" => IntakeEvidenceSource.FileName,
        "mime_type" => IntakeEvidenceSource.MimeType,
        "staff_correction" => IntakeEvidenceSource.StaffCorrection,
        "system_default" => IntakeEvidenceSource.SystemDefault,
        "provider_declaration" => IntakeEvidenceSource.ProviderDeclaration,
        _ => throw new InvalidDataException(
            $"Unknown persisted intake evidence source '{code}'.")
    };
}
