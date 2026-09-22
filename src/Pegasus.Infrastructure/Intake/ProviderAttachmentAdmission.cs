using Pegasus.Core.Intake;
using Pegasus.Core.ProviderApi;

namespace Pegasus.Infrastructure.Intake;

/// <summary>Checks a Provider file against its declaration and the ordinary intake reader before custody.</summary>
internal sealed class ProviderAttachmentAdmission(
    MimeKitPdfPigOpenXmlIntakeSourceReader reader,
    TimeProvider timeProvider) : IProviderAttachmentAdmission
{
    public async Task RequireSupportedAsync(
        IReadOnlyList<ProviderSubmissionFile> files,
        CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            if (!MatchesDeclarationAndHeader(file))
            {
                throw new ProviderInstructionValidationException(
                    $"files[{file.Ordinal}]",
                    "The file type, media type and content do not agree or are unsupported.");
            }

            var result = await reader.ReadAsync(
                new IntakeSource(
                    file.FileName,
                    file.MediaType,
                    file.Content,
                    timeProvider.GetUtcNow(),
                    "provider-attachment-admission",
                    new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, $"provider-file:{file.Ordinal}")),
                cancellationToken);
            if (result.Status != IntakeSourceReadStatus.Readable)
            {
                throw new ProviderInstructionValidationException(
                    $"files[{file.Ordinal}]",
                    result.FailureReason ?? "The file could not be read by the intake reader.");
            }
        }
    }

    private static bool MatchesDeclarationAndHeader(ProviderSubmissionFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mediaType = file.MediaType.Trim().ToLowerInvariant();
        var bytes = file.Content.Span;
        return (extension, mediaType) switch
        {
            (".pdf", "application/pdf") => bytes.StartsWith("%PDF-"u8),
            (".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document") =>
                bytes.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
            (".doc", "application/msword") or (".msg", "application/vnd.ms-outlook") =>
                bytes.StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            (".jpg", "image/jpeg") or (".jpeg", "image/jpeg") =>
                bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
            (".png", "image/png") =>
                bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            (".eml", "message/rfc822") => true,
            (".mp4", "video/mp4") or (".mov", "video/quicktime") =>
                bytes.Length >= 8 && bytes[4..8].SequenceEqual("ftyp"u8),
            _ => false
        };
    }
}
