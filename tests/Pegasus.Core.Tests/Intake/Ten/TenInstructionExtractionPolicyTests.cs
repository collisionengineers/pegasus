using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Ten;

[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class TenInstructionExtractionPolicyTests
{
    // The recorded text of each mail holds only its headers, so the samples
    // prove the route their real senders take; the fact below reads a body.
    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("4bcccaa649d0-TEN_01.msg.txt", "TEN 01.msg", "aaf2aad0843e9af2dea3071c51d0dba3751001d38b9f9626de422c4c07b04f96")]
    [InlineData("9afa45fb6b13-TEN_02.msg.txt", "TEN 02.msg", "6bb564894574ce70f207f2b935ed17ce19c5687cd19c24196e33151530cae080")]
    [InlineData("435025713bc0-TEN_03.msg.txt", "TEN 03.msg", "7367847b975ce2df915976c6468f0fe97f1decf00470d014065d6cf2c1e18ec4")]
    [InlineData("668d648c409e-TEN_04.msg.txt", "TEN 04.msg", "1379514b350f47312fc3ea405033f4abe87f6babffea4e19c46f23c1e25e4b44")]
    [InlineData("b1ea83e77f30-TEN_05.msg.txt", "TEN 05.msg", "1cf3dd85a35c30250eece4a684b204f3a8285bee1e2a3fa6b39feda4620ad3dc")]
    public void RecordedSendersTakeTheTenRoute(string extractedFile, string originalFile, string sha256)
    {
        var root = ReferencePack.Root();
        var original = File.ReadAllBytes(Path.Combine(root, "principal-docs", "original-mapper-instruction-corpus", originalFile));
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(original)));
        var from = File.ReadAllLines(Path.Combine(root, "astra_output", "extractions", "text", extractedFile))
            .Single(line => line.StartsWith("From: ", StringComparison.Ordinal));
        var sender = from[(from.IndexOf('<', StringComparison.Ordinal) + 1)..from.IndexOf('>', StringComparison.Ordinal)];

        var route = new PrincipalMailRoutePolicy().Evaluate(new(IntakeSourceReadStatus.Readable, [],
            [new(IntakeEvidenceSource.Sender, sender)], [], RequiresOcr: false));

        Assert.Equal(MailRouteDisposition.Accepted, route.Disposition);
        Assert.Equal("TEN", route.SelectedRoute?.PrincipalCode);
        Assert.Equal(MailRouteKind.DirectPrincipal, route.SelectedRoute?.Kind);
    }

    [Fact]
    public void TheLabelledBodySelectsExtractsAndClassifiesAnInspection()
    {
        // Invented values in Ten Legal's layout; the HTML body breaks the
        // line inside "Our Ref" as the recorded mails do.
        var read = new IntakeSourceReadResult(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.EmailBody, "ten.msg, message body", """
                    Our
                 Ref: AB.1234.VD
                  Client: Mr Ainsley Example
                  Vehicle Reg:   AB12 CDE
                  Accident Date: 03/03/2031
                Dear Sirs,
                Please inspect our client's vehicle and send us your report.
                For images please call the client on 0700 000 0000.
                Kind Regards
                A Handler
                handler@tenlegal.co.uk
                """)],
            [new(IntakeEvidenceSource.Sender, "handler@tenlegal.co.uk"),
             new(IntakeEvidenceSource.Subject, "Our Ref: AB.1234.VD  Client: Mr Ainsley Example")],
            [], RequiresOcr: false);
        var policies = typeof(IInstructionExtractionPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (IInstructionExtractionPolicy)Activator.CreateInstance(type)!)
            .ToArray();

        var selection = new InstructionExtractionPolicySelector(policies).Select(read, InstructionDocumentSignature.InstructionRole);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("TEN", selection.Policy!.PrincipalCode);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, new(2031, 3, 12, 9, 0, 0, TimeSpan.Zero),
            new("TEN", TenInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        Assert.Empty(result.MissingFields);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("Mr Ainsley Example", draft.ClaimantName);
        Assert.Equal("AB.1234.VD", draft.ClaimNumber);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2031, 3, 3), draft.DateOfIncident);

        var classification = new PrincipalMailClassificationPolicy("TEN").Classify(read, selection.InstructionContent);
        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }
}
