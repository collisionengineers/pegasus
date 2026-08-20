namespace Pegasus.Core.Intake;

/// <summary>
/// Projects the canonical reader's existing content fragments into one searchable
/// document per retained attachment; it does not parse source content itself.
/// </summary>
public static class IntakeSearchProjection
{
    public static IReadOnlyList<IntakeSearchDocument> Create(
        IntakeSourceReadResult readResult,
        MailRouteEvaluationResult? routeDecision)
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
        var effectiveSender = routeDecision?.EffectiveSender;
        var isStaffForward = effectiveSender is not null
            && routeDecision?.TransportIdentities.All(identity =>
                !string.Equals(
                    identity.Address,
                    effectiveSender.Address,
                    StringComparison.OrdinalIgnoreCase)) == true;
        List<IntakeContentFragment>? rootFragments = null;
        if (isStaffForward
            && effectiveSender is not null
            && grouped.TryGetValue(effectiveSender.SourceLabel, out var originalFragments))
        {
            rootFragments = originalFragments;
        }
        else
        {
            grouped.TryGetValue(string.Empty, out rootFragments);
        }
        if (rootFragments is not null)
        {
            var body = CombineSearchText(rootFragments);
            documents.Add(new(
                "message body",
                null,
                body is null ? null : StaffForwardBodyCleaner.Clean(body, isStaffForward)));
        }
        foreach (var descriptor in readResult.AttachmentRecords.OrderBy(item => item.Ordinal))
        {
            List<IntakeContentFragment>? fragments = null;
            if (descriptor.SourceLabel is not null)
            {
                grouped.TryGetValue(descriptor.SourceLabel, out fragments);
            }
            documents.Add(new(
                descriptor.SourceLabel ?? $"attachment {descriptor.Ordinal + 1}",
                descriptor.FileName,
                fragments is null ? null : CombineSearchText(fragments),
                descriptor.Ordinal));
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
