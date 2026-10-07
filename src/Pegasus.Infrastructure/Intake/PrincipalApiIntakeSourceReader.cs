using Pegasus.Core.Intake;
using Pegasus.Core.PrincipalApi;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// Recovers a Principal API submission's attachments from the retained request
/// body (API-01).
///
/// A submission is retained the way an e-mail is: one source — the request as
/// the Principal sent it — carrying its files inside. This reader is what makes
/// them attachments of that one receipt, so an instruction and the documents
/// that belong to it stay one job. Retaining each file as its own receipt
/// instead would scatter one instruction across many, and an Audit could not
/// then find its original report among its own assets.
///
/// It decorates the ordinary reader and defers to it for every other channel;
/// nothing about e-mail, upload or automation reading changes here.
/// </summary>
internal sealed class PrincipalApiIntakeSourceReader(IIntakeSourceReader inner) : IIntakeSourceReader
{
    public async Task<IntakeSourceReadResult> ReadAsync(
        IntakeSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceIdentity.Channel != IntakeSourceChannel.PrincipalApi)
        {
            return await inner.ReadAsync(source, cancellationToken);
        }

        try
        {
            var files = PrincipalInstructionJson.ParseFiles(source.Content);
            return new(
                IntakeSourceReadStatus.Readable,
                // No text is derived from a declaration: every value was stated,
                // and inventing content fragments here would let extraction
                // appear to have read something it never read.
                [],
                [],
                [],
                RequiresOcr: false,
                Assets: files
                    .Select(file => new IntakeAssetCandidate(
                        file.SourceLabel,
                        file.FileName,
                        file.MediaType,
                        file.Content,
                        IntakeAssetKind.Attachment,
                        IntakeAssetDisposition.Attachment))
                    .ToArray(),
                ReaderKey: PrincipalInstructionPolicy.ReaderKey,
                ReaderVersion: PrincipalInstructionPolicy.ReaderVersion,
                Attachments: files
                    .Select((file, ordinal) => new IntakeAttachmentDescriptor(
                        file.FileName,
                        file.MediaType,
                        file.Content.Length,
                        ordinal,
                        file.SourceLabel))
                    .ToArray());
        }
        catch (PrincipalInstructionValidationException exception)
        {
            // The body was accepted at the door and has since become unreadable.
            // That is a fault worth surfacing as one, not a malformed Principal
            // request: the request was parsed before it was retained.
            return new(
                IntakeSourceReadStatus.TechnicalFailure,
                [],
                [],
                [],
                RequiresOcr: false,
                FailureCode: "principal_submission_unreadable",
                FailureReason: $"The retained Principal submission could not be read: {exception.Field}.",
                ReaderKey: PrincipalInstructionPolicy.ReaderKey,
                ReaderVersion: PrincipalInstructionPolicy.ReaderVersion);
        }
    }
}
