using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Dfd;

/// <summary>
/// The live DFD instruction form as the reader presents it (the receipts of
/// 6 and 9 November 2026 behind R773 and R774): the printed labels as one page
/// fragment and each filled field as its own form-field fragment. The labels
/// and field names are the form's; every value is synthetic. The contact field
/// <c>Text9</c> is blank on the form and so, like any whitespace-only field,
/// is not emitted by the reader.
/// </summary>
internal static class DfdLiveFormFixture
{
    public const string EmailLabel = "uploaded dfd.eml";
    public const string FormLabel = EmailLabel + ", attachment 1: Collision Engineers instruction request form.pdf";
    public const string PrintedLabels =
        "Engineer instruction request\nInstructing principal\nYour Reference\nDate instructed\n"
        + "Client name or vehicle owner\nAccident date\nRegistration number\nClient contact number\n"
        + "(Below is optional)\nVehicle location\nAccident circumstances\nArea of damage\nClaim source\n"
        + "Contact details if not client\nAdditional information";

    private static readonly (string Name, string Value)[] FilledFields =
    [
        ("Text3", "Davison Flynn Duke Solicitors "),
        ("Text4", "RJP/00000"),
        ("Text6", "A Example"),
        ("Text8", "AB12CDE"),
        ("Text10", "A Garage"),
        ("Text11", "Example circumstances"),
        ("Text12", "o/s"),
        ("Text13", "A Source"),
        ("Text14", "A Contact")
    ];

    /// <summary>The page fragment and the filled fields, plus any further fields the test fills.</summary>
    public static IntakeContentFragment[] Content(params (string Name, string Value)[] furtherFields) =>
    [
        new(IntakeEvidenceSource.PdfContent, FormLabel + ", page 1", PrintedLabels, IntakeSourceLocator.ForPage(1)),
        .. FilledFields.Concat(furtherFields).Select(field => new IntakeContentFragment(
            IntakeEvidenceSource.PdfContent,
            $"{FormLabel}, form field {field.Name}",
            field.Value,
            IntakeSourceLocator.ForFormField(field.Name, 1)))
    ];
}
