using Pegasus.Core.Intake;
using Pegasus.Core.PrincipalApi;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// Checks a Principal file before custody. A document is opened by the
/// ordinary intake reader now, because nothing parses it again before it
/// reaches a Case; the reader does not open an image or a video, so those are
/// checked by their leading bytes instead.
/// </summary>
internal sealed class PrincipalAttachmentAdmission(
    MimeKitPdfPigOpenXmlIntakeSourceReader reader,
    TimeProvider timeProvider) : IPrincipalAttachmentAdmission
{
    public async Task RequireSupportedAsync(
        IReadOnlyList<PrincipalSubmissionFile> files,
        CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            if (HeaderMatches(file) is { } matches)
            {
                if (!matches)
                {
                    throw new PrincipalInstructionValidationException(
                        file.Field,
                        "The file content does not match its file type.");
                }

                continue;
            }

            var result = await reader.ReadAsync(
                new IntakeSource(
                    file.FileName,
                    file.MediaType,
                    file.Content,
                    timeProvider.GetUtcNow(),
                    "principal-attachment-admission",
                    new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, file.SourceLabel)),
                cancellationToken);
            if (result.Status != IntakeSourceReadStatus.Readable)
            {
                throw new PrincipalInstructionValidationException(
                    file.Field,
                    result.FailureReason ?? "The file could not be read by the intake reader.");
            }
        }
    }

    /// <summary>Whether an image or a video starts as its type does; null for a document.</summary>
    private static bool? HeaderMatches(PrincipalSubmissionFile file)
    {
        var bytes = file.Content.Span;
        return file.MediaType switch
        {
            "image/jpeg" => bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
            "image/png" => bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            IntakeUploadFilePolicy.Mp4MediaType or IntakeUploadFilePolicy.MovMediaType =>
                IntakeUploadFilePolicy.IsAccepted(file.FileName, file.MediaType, bytes),
            _ => null
        };
    }
}
