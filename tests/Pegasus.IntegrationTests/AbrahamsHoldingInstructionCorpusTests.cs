using System.Globalization;
using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The real letters behind Abrahams Solicitors' route and profile (R979):
/// every e-mail from their domain held under <c>corpus/holding/</c> by the
/// pegasus-corpus-holding skill goes through the real reader, the profile
/// selector, the profile and the classification policy. A letter the profile
/// selects must give the reference its own subject names and a claimant, and
/// classify as a new Inspection instruction. Held mail is local and ignored,
/// so this runs only where it has been copied; ordinary CI excludes the
/// Corpus category.
/// </summary>
public sealed class AbrahamsHoldingInstructionCorpusTests(ITestOutputHelper output)
{
    [AbrahamsHoldingCorpusFact]
    [Trait("Category", "Corpus")]
    public async Task EveryHeldLetterSelectsItsProfileExtractsAndClassifiesAnInspection()
    {
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var selector = new InstructionExtractionPolicySelector(typeof(IInstructionExtractionPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (IInstructionExtractionPolicy)Activator.CreateInstance(type)!)
            .ToArray());
        var policy = new PrincipalMailClassificationPolicy(AbrahamsInstructionExtractionPolicy.SupportedPrincipalCode);
        var receivedAt = TimeProvider.System.GetUtcNow();
        int classified = 0, notSelected = 0;

        foreach (var path in HoldingCorpus.FilesFrom(AbrahamsHoldingCorpusFactAttribute.SenderDomain))
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
                    "abrahams-holding-corpus",
                    new(IntakeSourceChannel.ManualUpload, $"holding-{sha[..12]}")),
                CancellationToken.None);
            var selection = read.Status == IntakeSourceReadStatus.Readable
                ? selector.Select(read, InstructionDocumentSignature.InstructionRole)
                : InstructionPolicySelection.NotApplicable();
            if (selection.Outcome != InstructionPolicySelectionOutcome.Selected)
            {
                // A reply or a chaser carries no letter; two profiles claiming one would be Ambiguous.
                Assert.NotEqual(InstructionPolicySelectionOutcome.Ambiguous, selection.Outcome);
                notSelected++;
                output.WriteLine($"{day} {sha[..12]} no letter selected: {read.Status}");
                continue;
            }

            Assert.Equal(AbrahamsInstructionExtractionPolicy.SupportedPrincipalCode, selection.Policy!.PrincipalCode);
            var draft = Assert.IsType<InstructionDraft>(selection.Policy.Extract(
                read with { Content = selection.InstructionContent },
                receivedAt,
                new(AbrahamsInstructionExtractionPolicy.SupportedPrincipalCode, PrincipalMailRoutePolicy.Key, PrincipalMailRoutePolicy.Version)).InstructionDraft);
            var subject = read.TransportEvidence.First(item => item.Source == IntakeEvidenceSource.Subject).Value;
            Assert.False(string.IsNullOrWhiteSpace(draft.ClaimantName), $"{sha[..12]}: no claimant");
            Assert.False(string.IsNullOrWhiteSpace(draft.ClaimNumber), $"{sha[..12]}: no reference");
            Assert.Contains(draft.ClaimNumber!, subject, StringComparison.Ordinal);
            Assert.Null(draft.VehicleRegistration);

            var classification = policy.Classify(read, selection.InstructionContent);
            Assert.True(
                classification.Outcome == MailClassificationOutcome.Classified,
                $"{sha[..12]}: {classification.Reason}");
            Assert.Equal(MailCategory.NewInstruction, classification.Category);
            Assert.Equal(CaseType.Inspection, classification.CaseType);
            classified++;
            output.WriteLine($"{day} {sha[..12]} new-instruction / Inspection, accident date {(draft.DateOfIncident is null ? "absent" : "read")}");
        }

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"held {classified + notSelected}: classified {classified}, without a selected letter {notSelected}"));
        Assert.True(classified > 0, "No held Abrahams letter was selected; the corpus proves nothing.");
    }
}

internal sealed class AbrahamsHoldingCorpusFactAttribute : FactAttribute
{
    public const string SenderDomain = "@abrahamssolicitors.co.uk";

    public AbrahamsHoldingCorpusFactAttribute()
    {
        if (HoldingCorpus.FilesFrom(SenderDomain).Length == 0)
        {
            Skip = "No Abrahams Solicitors mail is held under corpus/holding/ on this machine; the pegasus-corpus-holding skill copies it.";
        }
    }
}
