namespace Pegasus.Core.Intake;

/// <summary>
/// Projects the canonical reader's existing content fragments into one searchable
/// document per retained attachment; it does not parse source content itself.
/// </summary>
public static class IntakeSearchProjection
{
    public static IReadOnlyList<IntakeSearchDocument> Create(IntakeSourceReadResult readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);

        var attachments = readResult.AssetCandidates
            .Where(item => item.Disposition == IntakeAssetDisposition.Attachment)
            .OrderByDescending(item => item.SourceLabel.Length)
            .ToArray();
        var grouped = new Dictionary<string, List<IntakeContentFragment>>(StringComparer.Ordinal);
        foreach (var fragment in readResult.Content.Where(item => !string.IsNullOrWhiteSpace(item.Text)))
        {
            var attachment = attachments.FirstOrDefault(item =>
                fragment.SourceLabel.Equals(item.SourceLabel, StringComparison.Ordinal)
                || fragment.SourceLabel.StartsWith(item.SourceLabel + ",", StringComparison.Ordinal));
            var key = attachment?.SourceLabel ?? string.Empty;
            if (!grouped.TryGetValue(key, out var fragments))
            {
                fragments = [];
                grouped.Add(key, fragments);
            }
            fragments.Add(fragment);
        }

        var documents = new List<IntakeSearchDocument>();
        if (grouped.TryGetValue(string.Empty, out var rootFragments))
        {
            documents.Add(new("message body", null, CombineSearchText(rootFragments)));
        }
        foreach (var attachment in attachments.OrderBy(item => item.SourceLabel, StringComparer.Ordinal))
        {
            grouped.TryGetValue(attachment.SourceLabel, out var fragments);
            documents.Add(new(
                attachment.SourceLabel,
                attachment.FileName,
                fragments is null ? null : CombineSearchText(fragments)));
        }
        return documents;
    }

    private static string? CombineSearchText(IReadOnlyList<IntakeContentFragment> fragments)
    {
        var text = string.Join(
            Environment.NewLine,
            fragments.Select(item => item.Text.Trim()).Where(item => item.Length > 0));
        return text.Length == 0 ? null : text;
    }
}
