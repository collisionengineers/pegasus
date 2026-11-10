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
    private static bool? HeaderMatches(PrincipalSubmissionFile file) =>
        file.MediaType switch
        {
            // A PDF's signature is checked by the reader that opens it, as before.
            "application/pdf" => null,
            _ => IntakeUploadFilePolicy.HeaderMatches(file.FileName, file.MediaType, file.Content.Span)
        };
}
