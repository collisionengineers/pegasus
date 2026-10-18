using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Kerr;

[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class KerrInstructionExtractionPolicyTests
{
    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("fcb95ed60e14-KERR_01.msg.txt", "KERR 01.msg", "73c0d6cdaac90c7392c24ed7ec1f38fd30e45ec664b4fcccadfaff0bbebe550f", "AD/VRL/1/5241")]
    [InlineData("3afafee7c3f7-KERR_02.msg.txt", "KERR 02.msg", "ee086c4a796f271f1b1d773784fa86d3f09736cc90f99e41056d007768d2cf41", "AD/SR/VRL/1/5254")]
    [InlineData("894a5a2c0974-KERR_03.msg.txt", "KERR 03.msg", "60807a55b75d7511e98bb721f286a113f7662926444e5002c325b2e6bba2a2c2", "SR/VRL/1/5257")]
    [InlineData("b5068543605d-KERR_04.msg.txt", "KERR 04.msg", "754e165a6d9c0075aef20c06c3394e993aec615e1d1e95720e8fc7a8afef8db4", "SR/VRL/1/5274")]
    [InlineData("cac7ffc9465f-KERR_05.msg.txt", "KERR 05.msg", "78813ea0d2fa5bb10e73d2cc4c623ccdd0ca0e24738de5e97256890320306dc7", "CH/LEG/3/3818")]
    public void RecordedSubjectsCarryTheFileReference(string extractedFile, string originalFile, string sha256, string reference)
    {
        var root = ReferencePack.Root();
        var original = File.ReadAllBytes(Path.Combine(root, "principal-docs", "original-mapper-instruction-corpus", originalFile));
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(original)));
        var subject = File.ReadAllLines(Path.Combine(root, "astra_output", "extractions", "text", extractedFile))
            .Single(line => line.StartsWith("Subject: ", StringComparison.Ordinal))["Subject: ".Length..];

        Assert.Equal([reference], KerrInstructionExtractionPolicy.SubjectReferences(subject));
    }

    [Fact]
    public void TheLabelledBodyAndSubjectReferenceSelectExtractAndClassifyAnInspection()
    {
        // Invented values in Kerr Brown's layout; the date's suffix stands
        // apart as the HTML body renders a superscript.
        var read = new IntakeSourceReadResult(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.EmailBody, "kerr.msg, message body", """
                Good afternoon,
                Client:      Mr Ainsley Example
                Address:     1 Example Row, Exampleton, EX1 2PL
                Tele:        0700 000 0000
                Vehicle:     Example Motors Roadster 1.0 3dr 2031 green
                Reg:         AB12 CDE
                RTA:         3 rd  March 2031
                TPI:         Example Mutual
                Please arrange to inspect the vehicle at the client's address.
                Send the report and fee note to handler@kerrbrown.co.uk.
                Kind Regards,
                A Handler
                www.kerrbrown.co.uk
                """)],
            [new(IntakeEvidenceSource.Sender, "handler@kerrbrown.co.uk"),
             new(IntakeEvidenceSource.Subject, "NEW INSTRUCTIONS - Exampleton - Mr Ainsley Example   Our ref: AB/XY/1/1234")],
            [], RequiresOcr: false);
        var policies = typeof(IInstructionExtractionPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (IInstructionExtractionPolicy)Activator.CreateInstance(type)!)
            .ToArray();

        var selection = new InstructionExtractionPolicySelector(policies).Select(read, InstructionDocumentSignature.InstructionRole);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("KERR", selection.Policy!.PrincipalCode);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, new(2031, 3, 10, 9, 0, 0, TimeSpan.Zero),
            new("KERR", KerrInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("Mr Ainsley Example", draft.ClaimantName);
        Assert.Equal("AB/XY/1/1234", draft.ClaimNumber);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal("Example Motors Roadster 1.0 3dr 2031 green", draft.VehicleMake);
        Assert.Equal(new DateOnly(2031, 3, 3), draft.DateOfIncident);
        Assert.Null(draft.InspectionAddress);
        Assert.Empty(result.MissingFields);

        var classification = new PrincipalMailClassificationPolicy("KERR").Classify(read, selection.InstructionContent);
        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }
}
