using System.Security.Cryptography;
using System.Text;
using MimeKit;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.JevMailEvaluation;

public static class CorpusSampler
{
    private static readonly IReadOnlyDictionary<string, int> Quotas = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["body"] = 20,
        ["document"] = 20,
        ["image"] = 10,
        ["hard"] = 10
    };

    public static async Task<IReadOnlyList<CohortEntry>> PrepareAsync(
        string corpusRoot,
        int count = 60,
        int seed = 20260922,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(corpusRoot);
        var byHash = new Dictionary<string, CohortEntry>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, "*.eml", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (byHash.ContainsKey(hash))
            {
                continue;
            }

            var (stratum, attachmentCount) = Inspect(bytes);
            byHash.Add(hash, new(
                hash[..16].ToLowerInvariant(),
                Path.GetRelativePath(root, path),
                hash,
                stratum,
                bytes.LongLength,
                attachmentCount));
        }

        var ranked = byHash.Values
            .OrderBy(entry => Rank(entry.Sha256, seed), StringComparer.Ordinal)
            .ToArray();
        var selected = new List<CohortEntry>(count);
        foreach (var quota in Quotas)
        {
            selected.AddRange(ranked.Where(entry => entry.Stratum == quota.Key).Take(quota.Value));
        }

        selected.AddRange(ranked.Where(entry => !selected.Contains(entry)).Take(Math.Max(0, count - selected.Count)));
        return selected.Take(count).OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray();
    }

    private static (string Stratum, int AttachmentCount) Inspect(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var message = MimeMessage.Load(stream);
        var attachments = message.Attachments.ToArray();
        var subject = message.Subject ?? string.Empty;
        if (bytes.Length < 1024
            || subject.StartsWith("RE:", StringComparison.OrdinalIgnoreCase)
            || subject.StartsWith("FW:", StringComparison.OrdinalIgnoreCase)
            || subject.StartsWith("FWD:", StringComparison.OrdinalIgnoreCase)
            || subject.StartsWith("Automatic reply", StringComparison.OrdinalIgnoreCase))
        {
            return ("hard", attachments.Length);
        }

        if (attachments.Any(part => part.ContentType.MimeType is "application/pdf"
            or "application/msword"
            or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            or "text/plain"))
        {
            return ("document", attachments.Length);
        }

        return attachments.Any(part => part.ContentType.MediaType.Equals("image", StringComparison.OrdinalIgnoreCase))
            ? ("image", attachments.Length)
            : ("body", attachments.Length);
    }

    private static string Rank(string hash, int seed) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{hash}")));
}

public sealed class EvidenceStateBuilder
{
    public const int BodyCharacterLimit = 24_000;
    public const int AttachmentCharacterLimit = 16_000;
    public const int AllAttachmentsCharacterLimit = 40_000;
    private readonly IIntakeSourceReader reader;

    public EvidenceStateBuilder(IIntakeSourceReader? reader = null) =>
        this.reader = reader ?? new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);

    public async Task<JevMailState> BuildAsync(string path, bool omitAttachments, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var source = new IntakeSource(
            Path.GetFileName(path),
            "message/rfc822",
            bytes,
            DateTimeOffset.UnixEpoch,
            "jev-mail-evaluation",
            new(IntakeSourceChannel.ManualUpload, hash));
        var result = await reader.ReadAsync(source, cancellationToken);
        if (result.Status != IntakeSourceReadStatus.Readable)
        {
            throw new InvalidDataException($"The intake reader returned {result.Status}: {result.FailureCode} {result.FailureReason}".Trim());
        }

        using var stream = new MemoryStream(bytes, writable: false);
        var message = await MimeMessage.LoadAsync(stream, cancellationToken);
        var root = result.RootEmail;
        var body = root?.BodyPlainText ?? result.Content.FirstOrDefault(item => item.Source == IntakeEvidenceSource.EmailBody)?.Text;
        var boundedBody = Bound(body, BodyCharacterLimit);
        var attachments = omitAttachments ? [] : BuildAttachments(result);
        return new(
            root?.SenderAddress ?? message.From.Mailboxes.FirstOrDefault()?.Address,
            root?.ToAddresses ?? message.To.Mailboxes.Select(item => item.Address).ToArray(),
            root?.CcAddresses ?? message.Cc.Mailboxes.Select(item => item.Address).ToArray(),
            root?.Subject ?? message.Subject,
            boundedBody.Text,
            boundedBody.Truncated,
            message.InReplyTo,
            message.References.ToArray(),
            attachments,
            result.RequiresOcr,
            result.IsIncomplete,
            result.Issues.Select(issue => $"{issue.Code}: {issue.Reason}").ToArray(),
            omitAttachments);
    }

    internal static List<EvidenceAttachment> BuildAttachments(IntakeSourceReadResult result)
    {
        var remaining = AllAttachmentsCharacterLimit;
        var fragments = result.Content
            .Where(item => item.Source is IntakeEvidenceSource.PdfContent or IntakeEvidenceSource.DocumentContent)
            .GroupBy(item => item.SourceLabel, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => string.Join("\n", group.Select(item => item.Text)), StringComparer.Ordinal);
        var attachments = new List<EvidenceAttachment>();
        foreach (var descriptor in result.AttachmentRecords.OrderBy(item => item.Ordinal))
        {
            var text = descriptor.SourceLabel is not null && fragments.TryGetValue(descriptor.SourceLabel, out var exact)
                ? exact
                : fragments.FirstOrDefault(pair => pair.Key.Contains(descriptor.FileName, StringComparison.OrdinalIgnoreCase)).Value;
            var limit = Math.Min(AttachmentCharacterLimit, remaining);
            var bounded = Bound(text, limit);
            remaining -= bounded.Text?.Length ?? 0;
            attachments.Add(new(descriptor.FileName, descriptor.MediaType, descriptor.ContentLength, bounded.Text, bounded.Truncated));
        }
        return attachments;
    }

    internal static (string? Text, bool Truncated) Bound(string? value, int limit)
    {
        if (string.IsNullOrEmpty(value) || limit <= 0)
        {
            return (null, !string.IsNullOrEmpty(value));
        }
        return value.Length <= limit ? (value, false) : (value[..limit], true);
    }
}

public sealed class CohortReviewer(TextReader input, TextWriter output, EvidenceStateBuilder stateBuilder)
{
    private static readonly string[] NoSubtype = ["(none)"];

    public async Task ReviewAsync(string corpusRoot, string cohortPath, string labelsPath, CancellationToken cancellationToken = default)
    {
        var cohort = EvaluationJson.ReadJsonLines<CohortEntry>(cohortPath).ToArray();
        var labels = File.Exists(labelsPath)
            ? EvaluationJson.ReadJsonLines<GoldLabel>(labelsPath).ToDictionary(item => item.Id, StringComparer.Ordinal)
            : new Dictionary<string, GoldLabel>(StringComparer.Ordinal);
        foreach (var entry in cohort.Where(item => !labels.ContainsKey(item.Id)))
        {
            var state = await stateBuilder.BuildAsync(CorpusPath.Resolve(corpusRoot, entry.SourcePath), false, cancellationToken);
            await output.WriteLineAsync($"\n[{entry.Id}] {entry.Stratum} | {state.Subject}");
            await output.WriteLineAsync((state.BodyPlainText ?? "(no body)")[..Math.Min(1200, state.BodyPlainText?.Length ?? 9)]);
            await output.WriteLineAsync($"Attachments: {string.Join(", ", state.Attachments.Select(item => item.FileName))}");
            var families = EvaluationTaxonomy.Received.Keys.Append(EvaluationTaxonomy.Abstain).ToArray();
            for (var index = 0; index < families.Length; index++)
            {
                await output.WriteLineAsync($"{index + 1}. {families[index]}");
            }
            var family = families[ReadChoice(families.Length)];
            string? subtype = null;
            if (family != EvaluationTaxonomy.Abstain && EvaluationTaxonomy.Received[family].Length > 0)
            {
                var subtypes = NoSubtype.Concat(EvaluationTaxonomy.Received[family]).ToArray();
                for (var index = 0; index < subtypes.Length; index++)
                {
                    await output.WriteLineAsync($"{index + 1}. {subtypes[index]}");
                }
                var selected = subtypes[ReadChoice(subtypes.Length)];
                subtype = selected == "(none)" ? null : selected;
            }
            await output.WriteAsync("Reason: ");
            var reason = input.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidDataException("A review reason is required.");
            }
            var label = new GoldLabel(entry.Id, family, subtype, family == EvaluationTaxonomy.Abstain, reason, DateTimeOffset.UtcNow.ToString("O"));
            EvaluationTaxonomy.Validate(label);
            labels.Add(entry.Id, label);
            await EvaluationJson.WriteJsonLinesAsync(labelsPath, labels.Values.OrderBy(item => item.Id, StringComparer.Ordinal), cancellationToken);
        }
    }

    private int ReadChoice(int count)
    {
        var raw = input.ReadLine();
        return int.TryParse(raw, out var value) && value >= 1 && value <= count
            ? value - 1
            : throw new InvalidDataException($"Choose a number between 1 and {count}.");
    }
}
