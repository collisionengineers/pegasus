using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Operations;

public sealed record StaffMailAttachmentOption(
    string Selection, string FileName, string MediaType, long ContentLength);

public sealed class StaffMailAttachmentSelectionException(string message)
    : InvalidOperationException(message);

public interface IStaffMailAttachmentResolver
{
    Task<IReadOnlyList<StaffMailAttachmentOption>> ListCaseAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffMailAttachment>> ResolveCaseAsync(
        ActionActor actor, Guid caseId, IReadOnlyList<string> selections,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffMailAttachmentOption>> ListIntakeAsync(
        ActionActor actor, Guid receiptId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffMailAttachment>> ResolveIntakeAsync(
        ActionActor actor, Guid receiptId, IReadOnlyList<string> selections,
        CancellationToken cancellationToken);
}

public sealed class StaffMailAttachmentResolver(IGetCase getCase, IGetIntake getIntake)
    : IStaffMailAttachmentResolver
{
    public async Task<IReadOnlyList<StaffMailAttachmentOption>> ListCaseAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
        (await ReadCaseAsync(actor, caseId, cancellationToken)).Select(ToOption).ToArray();

    public async Task<IReadOnlyList<StaffMailAttachment>> ResolveCaseAsync(
        ActionActor actor, Guid caseId, IReadOnlyList<string> selections,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selections);
        return Resolve(await ReadCaseAsync(actor, caseId, cancellationToken), selections);
    }

    public async Task<IReadOnlyList<StaffMailAttachmentOption>> ListIntakeAsync(
        ActionActor actor, Guid receiptId, CancellationToken cancellationToken) =>
        (await ReadIntakeAsync(actor, receiptId, cancellationToken)).Select(ToOption).ToArray();

    public async Task<IReadOnlyList<StaffMailAttachment>> ResolveIntakeAsync(
        ActionActor actor, Guid receiptId, IReadOnlyList<string> selections,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selections);
        return Resolve(await ReadIntakeAsync(actor, receiptId, cancellationToken), selections);
    }

    private async Task<IReadOnlyList<StaffMailAttachment>> ReadCaseAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken)
    {
        var details = await getCase.ExecuteAsync(new(caseId, actor), cancellationToken)
            ?? throw new StaffMailAttachmentSelectionException(
                "The selected Case attachments are no longer available.");
        return CaseFiles.Live(details.Documents)
            .Select(file => new StaffMailAttachment(
                file.Occurrence.DocumentId, file.Version.Id, file.Version.Sha256,
                file.Version.ContentLength, file.Version.FileName, file.Version.MediaType))
            .Where(IsSendable)
            .DistinctBy(SelectionOf, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<StaffMailAttachment>> ReadIntakeAsync(
        ActionActor actor, Guid receiptId, CancellationToken cancellationToken)
    {
        var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken)
            ?? throw new StaffMailAttachmentSelectionException(
                "The selected Triage attachments are no longer available.");
        return IntakeFileIdentity.Ordered(receipt)
            .Where(asset => asset.CustodyState == IncomingArtifactCustodyState.Confirmed)
            .Select(asset => new StaffMailAttachment(
                null, null, asset.ContentHash, asset.ContentLength,
                asset.FileName, asset.MediaType, asset.Id, receipt.Id))
            .Where(IsSendable)
            .DistinctBy(SelectionOf, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<StaffMailAttachment> Resolve(
        IReadOnlyList<StaffMailAttachment> available,
        IReadOnlyList<string> selections)
    {
        if (selections.Count == 0)
        {
            return [];
        }
        if (selections.Count != selections.Distinct(StringComparer.Ordinal).Count())
        {
            throw Changed();
        }

        var bySelection = available.ToDictionary(SelectionOf, StringComparer.Ordinal);
        var resolved = new List<StaffMailAttachment>(selections.Count);
        foreach (var selection in selections)
        {
            if (!bySelection.TryGetValue(selection, out var attachment))
            {
                throw Changed();
            }
            resolved.Add(attachment);
        }
        return resolved;
    }

    private static StaffMailAttachmentOption ToOption(StaffMailAttachment attachment) =>
        new(SelectionOf(attachment), attachment.FileName, attachment.MediaType,
            attachment.ContentLength);

    private static bool IsSendable(StaffMailAttachment attachment) =>
        attachment.ContentLength > 0
        && attachment.Sha256.Length == 64
        && !string.IsNullOrWhiteSpace(attachment.FileName)
        && !string.IsNullOrWhiteSpace(attachment.MediaType);

    private static string SelectionOf(StaffMailAttachment attachment)
    {
        var identity = attachment.IntakeAssetId is { } assetId
            ? $"I|{attachment.IntakeReceiptId:D}|{assetId:D}"
            : $"D|{attachment.DocumentId:D}|{attachment.VersionId:D}";
        return $"{identity}|{attachment.Sha256.ToUpperInvariant()}|{attachment.ContentLength.ToString(CultureInfo.InvariantCulture)}";
    }

    private static StaffMailAttachmentSelectionException Changed() =>
        new("One or more selected attachments changed or are no longer available. Review the attachments and try again.");
}
