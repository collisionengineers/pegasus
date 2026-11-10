using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The holdout for Baker &amp; Coleman's instruction tell (R829, R830): every
/// e-mail from their domain held under <c>corpus/holding/</c> by the
/// pegasus-corpus-holding skill goes through the real reader, the profile
/// selector and the classification policy. A letter the BC profile selects
/// outside a reply thread must classify as a new Inspection instruction.
/// Held mail is local and ignored, so this runs only where it has been
/// copied; ordinary CI excludes the Corpus category.
/// </summary>
public sealed class BcHoldingInstructionCorpusTests(ITestOutputHelper output)
{
    [BcHoldingCorpusFact]
    [Trait("Category", "Corpus")]
    public async Task EveryHeldLetterSelectsItsProfileAndClassifiesAnInspection()
    {
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var selector = new InstructionExtractionPolicySelector(Policies());
        var policy = new PrincipalMailClassificationPolicy("BC");
        var receivedAt = TimeProvider.System.GetUtcNow();
        int classified = 0, replies = 0, notSelected = 0, unreadable = 0;

        foreach (var path in BcHoldingCorpus.Files())
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var day = Path.GetFileName(path)[..10];
            var read = await reader.ReadAsync(
                new IntakeSource(
                    Path.GetFileName(path),
                    "message/rfc822",
                    bytes,
                    receivedAt,
                    "bc-holding-corpus",
                    new(IntakeSourceChannel.ManualUpload, $"holding-{sha[..12]}")),
                CancellationToken.None);
            if (read.Status != IntakeSourceReadStatus.Readable)
            {
                unreadable++;
                output.WriteLine($"{day} {sha[..12]} unreadable: {read.Status}");
                continue;
            }

            var selection = selector.Select(read, InstructionDocumentSignature.InstructionRole);
            if (selection.Outcome != InstructionPolicySelectionOutcome.Selected
                || selection.Policy!.PrincipalCode != BcInstructionExtractionPolicy.SupportedPrincipalCode)
            {
                notSelected++;
                output.WriteLine($"{day} {sha[..12]} no BC letter selected: {selection.Outcome}");
                continue;
            }

            var classification = policy.Classify(read, selection.InstructionContent);
            if (classification.Predicates.Single(predicate => predicate.Key == "subject.reply-context").Matched)
            {
                replies++;
                output.WriteLine($"{day} {sha[..12]} letter inside a reply thread: {classification.Outcome}");
                continue;
            }

            Assert.True(
                classification.Outcome == MailClassificationOutcome.Classified,
                $"{sha[..12]}: {classification.Reason}");
            Assert.Equal(MailCategory.NewInstruction, classification.Category);
            Assert.Equal(CaseType.Inspection, classification.CaseType);
            classified++;
            output.WriteLine($"{day} {sha[..12]} new-instruction / Inspection");
        }

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"held {classified + replies + notSelected + unreadable}: classified {classified}, inside reply threads {replies}, without a selected letter {notSelected}, unreadable {unreadable}"));
        Assert.True(
            classified > 0,
            "No held Baker & Coleman letter was selected outside a reply thread; the holdout proves nothing.");
    }

    private static IInstructionExtractionPolicy[] Policies() =>
        typeof(IInstructionExtractionPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (IInstructionExtractionPolicy)Activator.CreateInstance(type)!)
            .ToArray();
}

internal static class BcHoldingCorpus
{
    private const string SenderDomain = "@bakercoleman.co.uk";

    public static string[] Files()
    {
        var holding = Path.Combine(QdosCorpus.Root, "holding");
        if (!Directory.Exists(holding))
        {
            return [];
        }

        return Directory.EnumerateFiles(holding, "*.eml", SearchOption.AllDirectories)
            .Where(FromHeaderNamesTheSender)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    // Only the header block is read, and only its From line decides; a Baker &
    // Coleman address copied on someone else's message is not their mail.
    private static bool FromHeaderNamesTheSender(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[16 * 1024];
        var count = stream.Read(buffer, 0, buffer.Length);
        var lines = Encoding.Latin1.GetString(buffer, 0, count).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("From:", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(lines.Skip(index).Take(3))
                    .Contains(SenderDomain, StringComparison.OrdinalIgnoreCase);
            }

            if (lines[index].TrimEnd('\r').Length == 0)
            {
                return false;
            }
        }

        return false;
    }
}

internal sealed class BcHoldingCorpusFactAttribute : FactAttribute
{
    public BcHoldingCorpusFactAttribute()
    {
        if (BcHoldingCorpus.Files().Length == 0)
        {
            Skip = "No Baker & Coleman mail is held under corpus/holding/ on this machine; the pegasus-corpus-holding skill copies it.";
        }
    }
}
