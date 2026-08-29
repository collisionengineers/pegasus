using Pegasus.Core.Intake;
using Pegasus.Core.ProviderApi;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// Recovers a Provider API submission's attachments from the retained request
/// body (API-01).
///
/// A submission is retained the way an e-mail is: one source — the request as
/// the provider sent it — carrying its files inside. This reader is what makes
/// them attachments of that one receipt, so an instruction and the documents
/// that belong to it stay one job. Retaining each file as its own receipt
/// instead would scatter one instruction across many, and an Audit could not
/// then find its original report among its own assets.
///
/// It decorates the ordinary reader and defers to it for every other channel;
/// nothing about e-mail, upload or automation reading changes here.
/// </summary>
internal sealed class ProviderApiIntakeSourceReader(IIntakeSourceReader inner) : IIntakeSourceReader
{
    public async Task<IntakeSourceReadResult> ReadAsync(
        IntakeSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceIdentity.Channel != IntakeSourceChannel.ProviderApi)
        {
            return await inner.ReadAsync(source, cancellationToken);
        }

        try
        {
            var (_, files) = ProviderInstructionJson.Parse(source.Content);
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
                        ProviderInstructionPolicy.AssetSourceLabel(file.Ordinal, file.Role),
                        file.FileName,
                        file.MediaType,
                        file.Content,
                        IntakeAssetKind.Attachment,
                        IntakeAssetDisposition.Attachment))
                    .ToArray(),
                ReaderKey: ProviderInstructionPolicy.ReaderKey,
                ReaderVersion: ProviderInstructionPolicy.ReaderVersion,
                Attachments: files
                    .Select(file => new IntakeAttachmentDescriptor(
                        file.FileName,
                        file.MediaType,
                        file.Content.Length,
                        file.Ordinal,
                        ProviderInstructionPolicy.AssetSourceLabel(file.Ordinal, file.Role)))
                    .ToArray());
        }
        catch (ProviderInstructionValidationException exception)
        {
            // The body was accepted at the door and has since become unreadable.
            // That is a fault worth surfacing as one, not a malformed provider
            // request: the request was parsed before it was retained.
            return new(
                IntakeSourceReadStatus.TechnicalFailure,
                [],
                [],
                [],
                RequiresOcr: false,
                FailureCode: "provider_submission_unreadable",
                FailureReason: $"The retained provider submission could not be read: {exception.Field}.",
                ReaderKey: ProviderInstructionPolicy.ReaderKey,
                ReaderVersion: ProviderInstructionPolicy.ReaderVersion);
        }
    }
}
