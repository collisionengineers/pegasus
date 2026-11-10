using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Black;

/// <summary>
/// The live Blackstone Legal instruction e-mail as the reader presents it (the
/// engineers@ copies of 25 September to 15 October 2026 behind R832 and R833):
/// a one-line body, "Please see attached new instructions.", above the firm's
/// signature, and the attached letter "Letter - instructing Collision
/// engineers.pdf" as one page fragment. The letter's wording, labels and line
/// breaks are the firm's as the reader orders them, with the reference line
/// after the signature; every party detail is synthetic.
/// </summary>
internal static class BlackLiveLetterFixture
{
    public const string EmailLabel = "uploaded blackstone.eml";
    public const string LetterLabel = EmailLabel + ", attachment 2: Letter - instructing Collision engineers.pdf";
    public const string Reference = "261900SA";
    public const string NewInstructionsSubject = "Our ref: " + Reference + "   /  New instructions ";
    public const string OurClientSubject = "Our Client - A Example  /  Our Reference - " + Reference;
    public const string DashedVehicleLine = "Toyota Prius - AB12 CDE";

    public const string Body =
        "Dear Sirs,\n\nPlease see attached new instructions.\n\nKind regards,\n\nBlackstone Legal\n"
        + "Unit 18, Girlington Road\nBradford, BD8 9NL\n\nT: 01274 888 533\nE: info@blackstone-legal.co.uk\n"
        + "W: www.blackstone-legal.co.uk";

    /// <summary>The letter's instruction sentence, broken where the reader breaks the line.</summary>
    public const string InstructionSentence =
        "Please accept this correspondence as formal\n"
        + "instructions to inspect our clients vehicle and prepare a report detailing the costs of repair or total loss\n"
        + "value, if beyond economical repair. ";

    public static string Letter(string vehicleLine = DashedVehicleLine) =>
        " T: 01274 888 533\nE: info@blackstone-legal.co.uk\nW: www.blackstone-legal.co.uk \n"
        + "Unit 18, Girlington Road, Bradford, BD8 9NL \n"
        + "Blackstone Legal is a trading name of Blackstone Law Services Ltd being a limited company registered in England and Wales under company\n"
        + "number 15370750 with a registered office address of Unit 18, Girlington Road, Bradford, BD8 9NL. Blackstone Law Services Ltd is authorised\n"
        + "and regulated by the Solicitors Regulation Authority (SRA number 8007335).   \n"
        + "Dear Sirs,\n"
        + "Our client: Mr A Example\n"
        + "Accident date: 01/10/2026\n"
        + "We act for the above client who was involved in a road traffic accident and seeks recovery of damage\n"
        + "caused to their vehicle following the index accident. " + InstructionSentence + "\n"
        + "Our clients and the vehicle details are below: \n"
        + "Name:   Mr A Example \n"
        + "Address: 1 Example Street, Exampleton, EX1 1AA\n"
        + "Mobile:  07700900000\n"
        + "Vehicle: " + vehicleLine + "\n"
        + "Your instructions to prepare this report is for the benefit of the Court as per your obligation under the\n"
        + "Civil Procedure Rules. Your report will be provided to the third party and the Court therefore you may\n"
        + "be contacted with any questions regarding the contents of your report. \n"
        + "Should you have any queries please do not hesitate to contact me.  \n"
        + "Yours sincerely, \n"
        + "Blackstone Legal     \n"
        + "Our ref:  " + Reference + "\n"
        + "Date:  1 October 2026\n"
        + "Collision Engineers \n77-79 Hoylake Road \nMoreton \nWirral\nCH46 9PY \n"
        + "engineers@collisionengineers.co.uk  \n";

    /// <summary>The letter as the reader's one page fragment.</summary>
    public static IntakeContentFragment Page(string vehicleLine = DashedVehicleLine) =>
        new(IntakeEvidenceSource.PdfContent, LetterLabel + ", page 1", Letter(vehicleLine), IntakeSourceLocator.ForPage(1));
}
