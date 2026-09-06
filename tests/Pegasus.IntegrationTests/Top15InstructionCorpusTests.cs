using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The top-15 instruction profiles against their own immutable originals, read
/// through the real reader, selected by the real document-profile selector and
/// extracted by the real policy.
///
/// One row per original. A batch that adds a profile adds ROWS here — its
/// samples, their recorded hashes, the identity an independent labeller read
/// off the document, and the neighbouring values that must never be mistaken
/// for it. It does not add a test.
///
/// What this asserts, and deliberately no more:
///
/// <list type="number">
/// <item>Every cited original resolves under the reference pack and hashes to
/// what the pack records. An expectation about bytes nobody has is not
/// evidence.</item>
/// <item>The document, and nothing about how it arrived, selects the profile
/// the labeller assigned it.</item>
/// <item>Zero WRONG identity. Where the labeller read a claimant, a
/// reference, a registration or a date off the original, extraction either
/// agrees or has nothing to say; it never confidently says something else.
/// That is the acceptance gate each method file proposes.</item>
/// <item>No neighbouring party's, address's or date's value ever arrives as
/// the claimant's identity.</item>
/// </list>
///
/// What it deliberately does NOT assert is a coverage floor. Five samples per
/// principal prove examples, not production accuracy, and the implementation
/// plan is explicit that no accuracy threshold may be claimed without
/// operator-labelled holdouts. So recall, ambiguity and missing counts are
/// MEASURED and written to
/// <c>artifacts/evaluation/v1-intake/top15-instruction-corpus.md</c> as a
/// per-profile, per-field matrix for the owner to read, rather than being
/// turned into a number a passing test would imply had been accepted.
///
/// A sample that cannot be read completely is recorded INCONCLUSIVE with its
/// reason. Inconclusive is not a pass and is never counted as one.
/// </summary>
[Trait("Category", "Corpus")]
public sealed class Top15InstructionCorpusTests
{
    private static readonly DateTimeOffset ProcessedAtUtc =
        new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The least text a read must recover before its result means anything.
    /// Every original in this corpus is a page or more of correspondence; a
    /// couple of hundred characters is a floor no genuine one is near.
    /// </summary>
    private const int MinimumRecoveredCharacters = 200;

    /// <summary>
    /// The identity an independent labeller read off one original, in the
    /// canonical form the pipeline produces: a name and a reference as
    /// printed, a registration with its spacing removed, dates as dates.
    /// A null means the labeller recorded the field as absent or ambiguous —
    /// nothing is asserted about it beyond the negatives below.
    /// </summary>
    private sealed record ExpectedIdentity(
        string? ClaimantName,
        string? ClaimNumber,
        string? VehicleRegistration,
        DateOnly? DateOfIncident,
        DateOnly? InstructionDate);

    /// <summary>
    /// A value printed on the original under a DIFFERENT label, and the field
    /// it must never arrive as. <c>Field</c> null means it must not arrive as
    /// any of the identity fields.
    /// </summary>
    private sealed record NeighbouringValue(string? Field, string Value, string Why);

    private sealed record SampleExpectation(
        string Profile,
        string PackRelativePath,
        string Sha256,
        ExpectedIdentity Identity,
        NeighbouringValue[] Negatives);

    private sealed record FwExpectation(
        string PackRelativePath,
        string Sha256,
        string Claimant,
        string Reference,
        string Registration,
        string Vehicle,
        DateOnly IncidentDate,
        DateOnly InstructionDate,
        string AccidentLocation,
        string? InspectionLocation,
        string Circumstances);

    private sealed record QclExpectation(
        string PackRelativePath,
        string Sha256,
        string Claimant,
        string Reference,
        string Registration,
        string Vehicle,
        DateOnly IncidentDate,
        DateOnly InstructionDate,
        string Location);

    private sealed record OakExpectation(
        string PackRelativePath,
        string Sha256,
        string Claimant,
        string Reference,
        string Registration,
        string? Model,
        DateOnly IncidentDate,
        DateOnly InstructionDate,
        string InspectionAddress,
        string Circumstances,
        string Source);

    private const string CorpusRoot = "principal-docs/original-mapper-instruction-corpus";

    /// <summary>
    /// Batch 1: QDOS and PCH. The remaining thirteen profiles' rows are added
    /// by their own batches, against the same four assertions.
    ///
    /// The QDOS identity comes from each letter's clean page-one header. Three
    /// of the five originals have genuine source-level row shift in their
    /// details table — labels and values zippered from different rows,
    /// confirmed against the rendered pages — so the labeller used the header
    /// where it gave an unambiguous corroborating value and recorded the rest
    /// ambiguous rather than reassigning it. The scrambled rows are the
    /// negatives.
    /// </summary>
    private static readonly SampleExpectation[] Expectations =
    [
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 01.pdf",
            "21ad661ea450a7d05a082da8742f5ea0d6bb6917db5b5b290ab2dabe78c04ede",
            new("Ms Angela Feetham", "LR/ND/45143/1", "NG22FVH", new(2026, 5, 2), new(2026, 5, 6)),
            [
                new("Vehicle registration", "NJ63YOF", "TP Registration: the third party's."),
                new("Claimant name", "Ageas Insurance Limited", "TP Representative Name."),
                new(
                    "Vehicle description",
                    "NISSAN X-TRAIL TEKNA DCI",
                    "TP Vehicle: the third party's vehicle."),
                new(
                    "Accident circumstances",
                    "Damage Area",
                    "Damage is not an account of how the accident happened.")
            ]),
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 02.pdf",
            "854ad91f463010780fc91e9c08961546d05c5c6f29cdf8320ae0fdad52d94d91",
            new("Mr Timothy Lewis", "AKH/ND/45078/1", "GO13UCS", new(2026, 5, 1), new(2026, 5, 6)),
            [
                new("Vehicle registration", "RA75OZP", "TP Registration."),
                new(
                    "Vehicle description",
                    "VOLVO XC40 PLUS PRO B4 MHEV AUTO",
                    "TP Vehicle."),
                new("Claimant name", "AIG UK LTD", "TP Representative Name."),
                new(
                    "Accident circumstances",
                    "Damage Area",
                    "Damage is not an account of how the accident happened.")
            ]),
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 03.pdf",
            "f6961bc33ec46f3e6312f9818f12b1b116a1645407f7e342c0d41806be88efef",
            // The reference row prints "MW/45101/1"; the details table is row
            // shifted and the header is the ground truth.
            new("Mr Andrew Adams", "MW/45101/1", "PY07FWD", new(2026, 5, 2), new(2026, 5, 6)),
            [
                new(
                    "Vehicle registration",
                    "Wear and tear",
                    "The row-shifted TP Registration slot; not a registration despite the label."),
                new(
                    "Vehicle description",
                    "KIA SPORTAGE KX-1 CRDI",
                    "A TP vehicle description in the TP Representative slot."),
                new(null, "Undriveable", "Vehicle status, not an identity or a circumstance.")
            ]),
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 04.pdf",
            "c78c2dcf87c3f2949cbe446840cbaa17cd54098b89b9972a43a84309d2cdc56b",
            new("Mr Thomas Wilson", "MW/45117/1", "CK62TXA", new(2026, 5, 3), new(2026, 5, 6)),
            [
                new("Vehicle description", "VAUXHALL CORSA SPORT", "A TP vehicle description."),
                new(null, "Undriveable", "Vehicle status, not an identity or a circumstance.")
            ]),
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 05.pdf",
            "080bec20fd211188ca8e19404ff518ed8396c348e381c6d2a2fda53fd0f0af94",
            // "Our Ref" prints as "/45160/1" with no initials prefix, unlike
            // every other original. Flagged by the labeller rather than
            // corrected, so nothing is asserted about the reference here.
            new("Mr Jamie Elder", null, "FD70ONU", new(2026, 5, 2), new(2026, 5, 6)),
            [
                new(
                    "Vehicle description",
                    "PEUGEOT EXPERT S STANDARD BLUE HDI",
                    "A TP vehicle description."),
                new(
                    "Inspection address",
                    "Gordon Marshall Coachworks",
                    "A repairer address does not prove a physical inspection.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 01.DOC",
            "87181b81f0fd3c59001178be782bcdfdb0efd504bb311491329d50896dbb94a4",
            new("Mrs Adam Bielecka", "573942", "VN20XFC", new(2026, 3, 31), new(2026, 5, 6)),
            [
                new("Claim number", "MRPC0103479703-LS", "Insurer Policy No, a different party's."),
                new("Claimant name", "Hannah Hammill", "The sender of the instruction message."),
                new(null, "01/04/2026", "Hire Out Date: when a replacement car was supplied."),
                new(
                    "Claimant address",
                    "1210 Centre Park Square",
                    "The supplier's footer address.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 02.DOC",
            "66f29af7613f63cc6c8ce13286db9905e6456574ebdb8aa9308806ca494746bd",
            new("Ms Angela Abdallah", "573425", "XS02ANG", new(2026, 3, 20), new(2026, 5, 6)),
            [
                new("Claim number", "MS1000743098Y0", "Insurer Policy No."),
                new("Claimant name", "Hannah Hammill", "The sender of the instruction message."),
                new(null, "23/03/2026", "Hire Out Date.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 03.DOC",
            "e9242909d2e4be91e35a4c90ada904f2d832fca3e126d2ea224bc1d0cc4d6a27",
            new("Mr Daniel Broome", "572566", "BD69NJY", new(2026, 3, 4), new(2026, 5, 6)),
            [
                // The clearest evidence in the corpus that driver and claimant
                // are two roles: two different people, one surname.
                new("Claimant name", "Mrs Nicky Broome", "Driver: a separate labelled role."),
                new("Claim number", "P68716723-1", "Insurer Policy No."),
                new(
                    "Inspection address",
                    "in use",
                    "A statement about whether the car is driven, not a place.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 04.DOC",
            "a2fa3a75cb7aee3fcb634692dc8abc3cdebd56fa7be7fb5471f62439d1aeb80c",
            // A corporate claimant: the field must accept a company name.
            new("Westons Group Ltd", "574289", "BD22GZW", new(2026, 3, 3), new(2026, 5, 6)),
            [
                new(
                    "Claimant name",
                    "Miss Carolann Hughes",
                    "The driver of the corporate claimant's vehicle."),
                new("Claim number", "NM050028493", "Insurer Policy No.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 05.DOC",
            "e5e6a84abe062fc5130d77b950a7fb5465f46560eb99879fc9171f0e4bafca05",
            // One day earlier than the other four: a genuine variable field.
            new("Mr Junior Cover", "573923", "JR07CVR", new(2026, 3, 31), new(2026, 5, 5)),
            [
                new("Claim number", "LN92101512821", "Insurer Policy No."),
                new(null, "02/04/2026", "Hire Out Date.")
            ]),
        new(
            "FW",
            $"{CorpusRoot}/FW 01.msg",
            "448cb639dffea48acb76c3cc68d3457028e7c600c06caa4e166d236266503512",
            new("Mr Dan-gabriel Ilie", "29679-01", "BA69UMG", new(2026, 5, 5), new(2026, 5, 6)),
            [
                new("Claimant name", "MrMartin", "Third Party Name."),
                new("Vehicle registration", "FG19VFL", "Third Party Reg."),
                new("Vehicle make", "Ford TRANSIT CONNECT", "The third party's vehicle.")
            ]),
        new(
            "FW",
            $"{CorpusRoot}/FW 02.msg",
            "db2572722d94ead8f15337368eec36ea6cfaa72f1a62f1d07bb6fbfc7f8e4d50",
            new("Catalin Anghelache", "29626-01", "KS21JUW", new(2026, 4, 15), new(2026, 5, 6)),
            [
                new("Vehicle registration", "RE71KFD", "Third Party Reg."),
                new("Vehicle make", "Ford TRANSIT CONNECT", "The third party's vehicle.")
            ]),
        new(
            "FW",
            $"{CorpusRoot}/FW 03.msg",
            "34e712ed176dc36135ed4e55badea0ff3c9897a8ea6d25fbad976e590223e45f",
            new("Mr Mohammed Zafran", "29667-01", "RX66FLG", new(2026, 4, 30), new(2026, 5, 6)),
            [
                new("Vehicle registration", "VO69MKZ", "Third Party Reg."),
                new("Vehicle make", "Mercedes-Benz GLA", "The third party's vehicle.")
            ]),
        new(
            "FW",
            $"{CorpusRoot}/FW 04.msg",
            "071dd9a8f16c16b368d52742848fae65fb1d79912e176a4abc49163a76b724d1",
            new("Mr Mebrahtom Debesay", "29680-01", "KP22LRL", new(2026, 5, 5), new(2026, 5, 6)),
            [
                new("Vehicle registration", "BG06UYX", "Third Party Reg."),
                new("Vehicle make", "Ford KA COLLECTION", "The third party's vehicle.")
            ]),
        new(
            "FW",
            $"{CorpusRoot}/FW 05.msg",
            "cd6df4397aec439baa5223bb549a4e0579ab9e6c09fc935e5159f3ac10a5bfa3",
            new("Mr Yunus Mohammed Abdul Amin", "29674-01", "RE05XEX", new(2026, 5, 4), new(2026, 5, 5)),
            [
                new("Claimant name", "Asaad", "Third Party Name."),
                new("Vehicle registration", "AP10FBF", "Third Party Reg."),
                new("Vehicle make", "Toyota VERSO", "The third party's vehicle.")
            ]),
        new(
            "QCL",
            $"{CorpusRoot}/QCL 01.docx",
            "cb6b0f120d2e8d06a2e79e0b97f6326ab12395a1c9cfc60cc3f8ad6e1744bac7",
            new("Mr Hamza Ahmad", "225880.TA", "AY19LTW", new(2026, 5, 4), new(2026, 5, 6)),
            [
                new("Claimant name", "Complex Reports", "The intermediary named in the address block.")
            ]),
        new(
            "QCL",
            $"{CorpusRoot}/QCL 02.docx",
            "94f1c9c9af4881d20a18a6ba3819970273dc1c66ebe71b18f8d29ed6807131c7",
            new("Mr Masroor Amjad", "225882.TA", "SC67LBV", new(2026, 4, 29), new(2026, 5, 5)),
            [new("Claimant name", "Complex Reports", "The intermediary named in the address block.")]),
        new(
            "QCL",
            $"{CorpusRoot}/QCL 03.docx",
            "a0cc054eb8060172428cad0cc8621cd019c45efa929fb453d42960782dbff474",
            new("Mr Syed Azhar Hussain", "225873.TA", "LY63XKP", new(2026, 4, 30), new(2026, 5, 1)),
            [new("Claimant name", "Complex Reports", "The intermediary named in the address block.")]),
        new(
            "QCL",
            $"{CorpusRoot}/QCL 04.docx",
            "e37143b3f73ada187db9ff4e196778f40ddd4ca7f302508d5285cdcae7170a2e",
            new("Mr Bilal Hussain", "225871.TA", "FG70UJS", new(2026, 4, 30), new(2026, 5, 1)),
            [new("Claimant name", "Complex Reports", "The intermediary named in the address block.")]),
        new(
            "QCL",
            $"{CorpusRoot}/QCL 05.docx",
            "4cee9b8ef41dccdbc2db90685ec1fef3fc00eba13dd28da8b09d44503259433a",
            new("Mr Chaudhary Ameer", "225870.TA", "MX67PXS", new(2026, 4, 29), new(2026, 5, 1)),
            [
                new("Claimant name", "Complex Reports", "The intermediary named in the address block.")
            ]),
        new(
            "OAK",
            $"{CorpusRoot}/OAK 01.DOC",
            "2253a09ce674ef3e52548694f14d9b00e989789212acb210f4766ecd35979da7",
            new("Mr Sam Graham", "TJD/GRAHAM/S486562.001", "B24SRG", new(2026, 5, 5), new(2026, 5, 5)),
            [
                new("Claimant name", "O'malley Recovery", "The separately labelled Source/Introducer."),
                new("Claim reference", "05/05/26", "The aligned header Date is not Our Ref.")
            ]),
        new(
            "OAK",
            $"{CorpusRoot}/OAK 02.DOC",
            "22395559092263e89dd7440e61e26521a0f693e87355ccdaf5f64bae77b06d4e",
            new("Ms Anna Pachla", "TJD/PACHLA/S486035.001", "EN18KEJ", new(2026, 4, 27), new(2026, 5, 5)),
            [new("Claimant name", "Hfdrz Ltd Taxi", "The separately labelled Source/Introducer.")]),
        new(
            "OAK",
            $"{CorpusRoot}/OAK 03.DOC",
            "c48c8702830036066d21ddacc9f3d224a0bfe5db15d5d68ad45d76a42a46f19a",
            new("Mr Lewis Morgan", "JAA/MORGAN/S486439.001", "CV68OVM", new(2026, 5, 4), new(2026, 5, 5)),
            [new("Claimant name", "Wilson Breakdown Recovery", "The separately labelled Source/Introducer.")]),
        new(
            "OAK",
            $"{CorpusRoot}/OAK 04.DOC",
            "191ac025ab19d0174375e8bf831ea6083f1ed2ef61be182e4055fe01cd5cfaa2",
            new("Mr Mohammad Butt", "GHE/BUTT/S486424.001", "SG12BLS", new(2026, 5, 3), new(2026, 5, 5)),
            [new("Claimant name", "Undent It", "The separately labelled Source/Introducer.")]),
        new(
            "OAK",
            $"{CorpusRoot}/OAK 05.DOC",
            "70424671cf11e236e570db5bf0f806a23499d7d663f857f0a2c73c67e3c89b41",
            new("Mr James O'Donnell", "JPS/O'DONNELL/S486079.001", "MF17WYH", new(2026, 4, 17), new(2026, 5, 1)),
            [
                new("Claimant name", "Spray Tek Accident Repair Centre Ltd", "The separately labelled Source/Introducer."),
                new(null, "cost of replacement if beyond repair", "Requested work is not an accepted report outcome.")
            ]),
        new(
            "SBL", $"{CorpusRoot}/SBL 01.pdf",
            "fa2d7e6abe04830ac29bd5faa7b9452212a6bc91d636cfddf10510c821780fc8",
            new("Mr Craig Motorhome Escapes", "SBL-B0470099", "SK24KYF", new(2026, 4, 6), new(2026, 5, 6)),
            [new("Claimant name", "C.A.R.S Collision Accident Recovery Service Ltd", "Introducer, not policyholder."), new("Inspection address", "Block 1 Whiteside Industrial Estate, Bathgate EH48 2RX", "Repairer address has its own role.")]),
        new(
            "SBL", $"{CorpusRoot}/SBL 02.pdf",
            "7cd71550bb2d0d782885928036c23818b6db877cac97db30d55e76ca47d62866",
            new("Mr EDSB Ltd EDSB Ltd", "SBL-B0558371", "DA75JCU", new(2026, 4, 26), new(2026, 5, 6)),
            [new("Claimant name", "MAGNA ACCIDENT SERVICES LIMITED", "Introducer, not policyholder."), new("Inspection address", "Watling St Hinckley", "Repairer address has its own role.")]),
        new(
            "SBL", $"{CorpusRoot}/SBL 03.pdf",
            "d5106d2067f7576c6873527db59f8868ed8617f2f1334faf22f5a65caf36adee",
            new("Ms Jacklyn Gurney", "SBL-B0427818", "L777GUR", new(2026, 2, 12), new(2026, 4, 28)),
            [new("Claimant name", "C.A.R.S Collision Accident Recovery Service Ltd", "Introducer, not policyholder."), new("Inspection address", "36 Speirs Wharf, Glasgow G4 9TG", "Repairer address has its own role.")]),
        new(
            "SBL", $"{CorpusRoot}/SBL 04.pdf",
            "3fd4d9cd2f7895579f51afcaf055f43e465038125543f198046109fd313dd99b",
            new("Miss Arabella Christie", "SBL-B0423796", "AJ17FNL", new(2026, 4, 25), new(2026, 4, 28)),
            [new("Claimant name", "Fleet Mitigation Solutions", "Introducer, not policyholder."), new("Inspection address", "Unit C2, Rhymes Lane, Fairford, GL7 4BU", "Repairer address has its own role.")]),
        new(
            "SBL", $"{CorpusRoot}/SBL 05.pdf",
            "436db268cf7cb824ef089e08399879b4f7f78a65bf3c2b0f515d043c44bb3e00",
            new("Mr Yoni Sherer", "SBL-B0484837", "VX71YDO", new(2026, 4, 13), new(2026, 4, 28)),
            [new("Claimant name", "Parkhouse Assist", "Introducer, not policyholder."), new("Inspection address", "Unit 1, Leo Industrial Estate, Mosley Rd, Trafford Park, Stretford, Manchester M17 1JS", "Repairer address has its own role.")])
    ];

    private static readonly FwExpectation[] FwExpectations =
    [
        new(
            $"{CorpusRoot}/FW 01.msg",
            "448cb639dffea48acb76c3cc68d3457028e7c600c06caa4e166d236266503512",
            "Mr Dan-gabriel Ilie", "29679-01", "BA69UMG", "Toyota PRIUS",
            new(2026, 5, 5), new(2026, 5, 6), "Ashby Rd B5493 Near Kings Ln",
            "Somstar Recovery & Storage Land Of Rea Street & Moseley Street Birmingham B5 6JX 07462530375",
            "Our Client Was Travelling Along The Main Road Behind The Third Party 2 Vehicle. FH61EDO Who Slowed Due To A Broken Down Vehicle On The Left Hand Side And Oncoming Vehicles. Our Client Slowed , The Third Party Came From Behind, Hit Our Client In The Rear Which Shunted Our Client Into The Third Party 2 Vehicle. The Vehicle Front Reg: FH61EDO"),
        new(
            $"{CorpusRoot}/FW 02.msg",
            "db2572722d94ead8f15337368eec36ea6cfaa72f1a62f1d07bb6fbfc7f8e4d50",
            "Catalin Anghelache", "29626-01", "KS21JUW", "Mercedes-Benz E 220 AMG LNE NGT ED PRM + D A",
            new(2026, 4, 15), new(2026, 5, 6), "A3", null,
            "On April 15, 2026, I Was Traveling From Guildford To Gatwick. On The A3 - Junction 10 / Cobham - Wisley M25. There Are Two Lanes Allocated To Gatwick At Junction 10. At The Time Of The Incident, I Was In The Right-hand Lane In The Direction Of Travel. I Also Note That There Are Three Traffic Lights In The Same Direction. After The Second Traffic Light Changed Color To Green, I Continued Moving Forward At A Speed Of Approximately 5 Miles / H Because I Noticed That The Last Traffic Light Was Red. At That Moment, I Noticed That A Mini Van Appeared On My Left And Suddenly Entered In Front Of Me. My Only Reaction Was To Move My Car To The Right And Apply The Brakes. I Must Say That The Other Driver Not Only Had A Much Higher Speed But Also Did Not Signal His Intention To Change Direction. If I Hadn't Had The Presence Of Mind To Pull The Steering Wheel To The Right And Apply The Brake, I Think The Damage Would Have Been Greater."),
        new(
            $"{CorpusRoot}/FW 03.msg",
            "34e712ed176dc36135ed4e55badea0ff3c9897a8ea6d25fbad976e590223e45f",
            "Mr Mohammed Zafran", "29667-01", "RX66FLG", "Toyota PRIUS",
            new(2026, 4, 30), new(2026, 5, 6), "Soho Avenue, Brimingham", null,
            "Our Client Stopped behind a Vehicle in traffic when tp reversed and hit Our Clients Vehicle on the Front Driverside he reversed as he went over the lane for opposite traffic"),
        new(
            $"{CorpusRoot}/FW 04.msg",
            "071dd9a8f16c16b368d52742848fae65fb1d79912e176a4abc49163a76b724d1",
            "Mr Mebrahtom Debesay", "29680-01", "KP22LRL", "Toyota COROLLA DESIGN HEV CVT",
            new(2026, 5, 5), new(2026, 5, 6), "Water Orton Lane",
            "Somstar Recovery & Storage Land Of Rea Street & Moseley Street Birmingham B5 6JX 07462530375",
            "Our Client Was Driving In Their Lane When They Saw Another Vehicle Attempting To Overtake A Stationary Bus And And Move Into Their Path. Our Client Slowed Down And Came To A Stop To Avoid A Collision. However, Tp Continued Driving In Our Client's Lane And Hit Our Client's Vehicle Head On. Dashcam Footage Available."),
        new(
            $"{CorpusRoot}/FW 05.msg",
            "cd6df4397aec439baa5223bb549a4e0579ab9e6c09fc935e5159f3ac10a5bfa3",
            "Mr Yunus Mohammed Abdul Amin", "29674-01", "RE05XEX", "Honda CIVIC TYPE R",
            new(2026, 5, 4), new(2026, 5, 5), "Lancefield St",
            "Somstar Recovery & Storage Land Of Rea Street & Moseley Street Birmingham B5 6JX 07462530375",
            "Our Clients Vehicle Was Parked On Lancefield St When Tp Drove By And Hit His Rear Passenger Side On Our Clients Front Side Causing Damage To The Vehicle And Client Seen The Whole Thing As He Was Not Far From The Vehicle")
    ];

    private static readonly QclExpectation[] QclExpectations =
    [
        new(
            $"{CorpusRoot}/QCL 01.docx",
            "cb6b0f120d2e8d06a2e79e0b97f6326ab12395a1c9cfc60cc3f8ad6e1744bac7",
            "Mr Hamza Ahmad", "225880.TA", "AY19LTW", "BMW X3",
            new(2026, 5, 4), new(2026, 5, 6),
            "54 Street Austell Drive Heald Green Cheadle SK8 3EG"),
        new(
            $"{CorpusRoot}/QCL 02.docx",
            "94f1c9c9af4881d20a18a6ba3819970273dc1c66ebe71b18f8d29ed6807131c7",
            "Mr Masroor Amjad", "225882.TA", "SC67LBV", "Hyundai Ioniq",
            new(2026, 4, 29), new(2026, 5, 5),
            "8 Dunley Close Manchester M12 4TE"),
        new(
            $"{CorpusRoot}/QCL 03.docx",
            "a0cc054eb8060172428cad0cc8621cd019c45efa929fb453d42960782dbff474",
            "Mr Syed Azhar Hussain", "225873.TA", "LY63XKP", "Toyota Prius Hybrid",
            new(2026, 4, 30), new(2026, 5, 1),
            "Flat 5 Dale House 204 London Road Hazel Grove Stockport SK7 4DF"),
        new(
            $"{CorpusRoot}/QCL 04.docx",
            "e37143b3f73ada187db9ff4e196778f40ddd4ca7f302508d5285cdcae7170a2e",
            "Mr Bilal Hussain", "225871.TA", "FG70UJS", "Toyota Corolla Icon",
            new(2026, 4, 30), new(2026, 5, 1),
            "333 Brinnington Road Stockport SK5 8AF"),
        new(
            $"{CorpusRoot}/QCL 05.docx",
            "4cee9b8ef41dccdbc2db90685ec1fef3fc00eba13dd28da8b09d44503259433a",
            "Mr Chaudhary Ameer", "225870.TA", "MX67PXS", "Toyota Prius",
            new(2026, 4, 29), new(2026, 5, 1),
            "34 Avon Way Colchester CO4 3TP")
    ];

    private static readonly OakExpectation[] OakExpectations =
    [
        new($"{CorpusRoot}/OAK 01.DOC", "2253a09ce674ef3e52548694f14d9b00e989789212acb210f4766ecd35979da7", "Mr Sam Graham", "TJD/GRAHAM/S486562.001", "B24SRG", null, new(2026, 5, 5), new(2026, 5, 5), "17 Powdermill Brae, Gorebridge, EH23 4HX", "CL in the left lane of a roundabout then TP moved into CL's lane and hit CL's vehicle..", "O'malley Recovery"),
        new($"{CorpusRoot}/OAK 02.DOC", "22395559092263e89dd7440e61e26521a0f693e87355ccdaf5f64bae77b06d4e", "Ms Anna Pachla", "TJD/PACHLA/S486035.001", "EN18KEJ", null, new(2026, 4, 27), new(2026, 5, 5), "19 J Annandale Street, Edinburgh, EH21 7AH", "Client was driving her Taxi in the left lane. TP was going in same direction in right hand lane. Suddenly TP changed into the clients lane and collided with the clients vehicle. She thinks his intention was to turn left..", "Hfdrz Ltd Taxi"),
        new($"{CorpusRoot}/OAK 03.DOC", "c48c8702830036066d21ddacc9f3d224a0bfe5db15d5d68ad45d76a42a46f19a", "Mr Lewis Morgan", "JAA/MORGAN/S486439.001", "CV68OVM", null, new(2026, 5, 4), new(2026, 5, 5), "41 Moffat Crescent, Lochgelly, KY5 9NY", "CL stationary on the road due to traffic when the oncoming TP hit CL's vehicle and proceed down the road.", "Wilson Breakdown Recovery"),
        new($"{CorpusRoot}/OAK 04.DOC", "191ac025ab19d0174375e8bf831ea6083f1ed2ef61be182e4055fe01cd5cfaa2", "Mr Mohammad Butt", "GHE/BUTT/S486424.001", "SG12BLS", "TOYOTA YARIS VVT-I SR", new(2026, 5, 3), new(2026, 5, 5), "15 Greenacres Drive, Glasgow, G53 7BB", "that our client was proceeding correctly through a green light at a cross road when the defendant ran a red light, cutting across them to turn right, colliding with our client’s vehicle.", "Undent It"),
        new($"{CorpusRoot}/OAK 05.DOC", "70424671cf11e236e570db5bf0f806a23499d7d663f857f0a2c73c67e3c89b41", "Mr James O'Donnell", "JPS/O'DONNELL/S486079.001", "MF17WYH", null, new(2026, 4, 17), new(2026, 5, 1), "99 Littleton Park, Barrhead, Glasgow, G78 2FA", "Clients was progressing down the narrow road as the tp initially was stationary in the passing place (photos attached). Tp all of a sudden pulled out of the passing place giving the client no where to go and colliding with the side of clients vehicle also pushing the vehicle into the hedges..", "Spray Tek Accident Repair Centre Ltd")
    ];

    private static IInstructionExtractionPolicy[] Policies() =>
        [
            new QdosInstructionExtractionPolicy(),
            new PchInstructionExtractionPolicy(),
            new FwInstructionExtractionPolicy(),
            new QclInstructionExtractionPolicy(),
            new OakInstructionExtractionPolicy(),
            new SblInstructionExtractionPolicy()
        ];

    [ReferencePackFact]
    public async Task EveryLabelledOriginalSelectsItsProfileAndMisidentifiesNothing()
    {
        var root = PackRoot();
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var selector = new InstructionExtractionPolicySelector(Policies());
        var report = new StringBuilder()
            .AppendLine("# Top-15 instruction corpus: per-profile, per-field matrix")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Pack root read from `{PackRootVariable}`.")
            .AppendLine(
                "Recall and ambiguity are MEASURED here, not asserted: five samples per "
                + "principal prove examples, not production accuracy, and no accuracy "
                + "threshold is claimed without operator-labelled holdouts.")
            .AppendLine();

        var readable = 0;
        var inconclusive = new List<string>();
        var failures = new List<string>();
        var counts = new Dictionary<(string Profile, string Field, string Disposition), int>();

        foreach (var expectation in Expectations)
        {
            var absolute = Path.Combine(
                root, expectation.PackRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var name = Path.GetFileName(expectation.PackRelativePath);
            if (!File.Exists(absolute))
            {
                failures.Add($"{name}: the pack does not carry this original.");
                continue;
            }

            // Read once: the same bytes are hashed and handed to the reader, so
            // what was verified is what was measured.
            var bytes = await File.ReadAllBytesAsync(absolute);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!string.Equals(sha256, expectation.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{name}: hashes to {sha256}, and the pack records {expectation.Sha256}.");
                continue;
            }

            var readResult = await reader.ReadAsync(
                Source(bytes, name, sha256), CancellationToken.None);
            if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            {
                inconclusive.Add(
                    $"{name}: {readResult.FailureReason ?? "the reader returned incomplete content"}"
                    + " - INCONCLUSIVE, which is not a pass.");
                continue;
            }

            // A read that recovered almost nothing has given the profile
            // nothing to identify and the policy nothing to extract. That is a
            // reader gap, and calling it a misidentification would blame the
            // wrong thing - so it is recorded as inconclusive, by which nobody
            // is reassured.
            var recovered = readResult.Content.Sum(fragment => fragment.Text.Length);
            if (recovered < MinimumRecoveredCharacters)
            {
                inconclusive.Add(
                    $"{name}: the reader recovered {recovered} characters, below the "
                    + $"{MinimumRecoveredCharacters} this measurement needs - INCONCLUSIVE, "
                    + "which is not a pass.");
                continue;
            }

            readable++;
            var selection = selector.Select(
                readResult, InstructionDocumentSignature.InstructionRole);
            if (selection.Outcome != InstructionPolicySelectionOutcome.Selected
                || !string.Equals(
                    selection.Policy!.PrincipalCode, expectation.Profile, StringComparison.Ordinal))
            {
                failures.Add(
                    $"{name}: the document selected {Describe(selection)}, and the labeller "
                    + $"assigned it {expectation.Profile}.");
                continue;
            }

            var policy = selection.Policy!;
            var profile = (IInstructionDocumentProfile)policy;
            var result = policy.Extract(
                readResult,
                ProcessedAtUtc,
                new(policy.PrincipalCode, profile.DocumentProfileKey, profile.DocumentProfileVersion));

            AppendSample(report, name, sha256, expectation, selection, result, profile, policy);
            Count(counts, expectation.Profile, result);
            failures.AddRange(WrongIdentity(name, expectation, result));
            failures.AddRange(NeighbouringValuesThatArrived(name, expectation, result));
        }

        AppendMatrix(report, counts);
        if (inconclusive.Count > 0)
        {
            report.AppendLine().AppendLine("## Inconclusive").AppendLine();
            foreach (var line in inconclusive)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"- {line}");
            }
        }

        WriteReport(report.ToString());

        Assert.True(
            readable > 0,
            "No labelled original could be read completely, so nothing was measured.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [ReferencePackFact]
    public async Task FairwayOriginalsProduceExactCurrentInstructionFields()
    {
        var root = PackRoot();
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var policy = new FwInstructionExtractionPolicy();
        var selector = new InstructionExtractionPolicySelector([policy]);

        foreach (var expectation in FwExpectations)
        {
            var absolute = Path.Combine(
                root,
                expectation.PackRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var bytes = await File.ReadAllBytesAsync(absolute);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            Assert.Equal(expectation.Sha256, sha256);

            var read = await reader.ReadAsync(
                Source(bytes, Path.GetFileName(absolute), sha256),
                CancellationToken.None);
            Assert.Equal(IntakeSourceReadStatus.Readable, read.Status);
            Assert.False(read.IsIncomplete);
            var selection = selector.Select(read, InstructionDocumentSignature.InstructionRole);
            Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);

            var result = policy.Extract(
                read,
                ProcessedAtUtc,
                new("FW", policy.DocumentProfileKey, policy.DocumentProfileVersion));
            var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
            Assert.Equal(expectation.Claimant, draft.ClaimantName);
            Assert.Equal(expectation.Reference, draft.ClaimNumber);
            Assert.Equal(expectation.Registration, draft.VehicleRegistration);
            Assert.Equal(expectation.Vehicle, draft.VehicleMake);
            Assert.Equal(expectation.IncidentDate, draft.DateOfIncident);
            Assert.Equal(expectation.InstructionDate, draft.InstructionDate);
            Assert.Equal(
                expectation.AccidentLocation,
                Assert.Single(result.Fields, field => field.Name == "Accident location").SuggestedValue);
            Assert.Equal(expectation.InspectionLocation, draft.InspectionAddress);
            Assert.Equal(expectation.Circumstances, draft.AccidentCircumstances);
            Assert.DoesNotContain(result.Fields, field => field.HasConflict);
            Assert.All(result.Fields.SelectMany(field => field.Candidates), candidate =>
                Assert.Contains("message body", candidate.SourceLabel, StringComparison.OrdinalIgnoreCase));
        }
    }

    [ReferencePackFact]
    public async Task QcLawOriginalsProduceExactBoundedInstructionFields()
    {
        var root = PackRoot();
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var policy = new QclInstructionExtractionPolicy();
        var selector = new InstructionExtractionPolicySelector([policy]);

        foreach (var expectation in QclExpectations)
        {
            var absolute = Path.Combine(
                root,
                expectation.PackRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var bytes = await File.ReadAllBytesAsync(absolute);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            Assert.Equal(expectation.Sha256, sha256);

            var read = await reader.ReadAsync(
                Source(bytes, Path.GetFileName(absolute), sha256),
                CancellationToken.None);
            Assert.Equal(IntakeSourceReadStatus.Readable, read.Status);
            Assert.False(read.IsIncomplete);
            var selection = selector.Select(read, InstructionDocumentSignature.InstructionRole);
            Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);

            var result = policy.Extract(
                read,
                ProcessedAtUtc,
                new("QCL", policy.DocumentProfileKey, policy.DocumentProfileVersion));
            var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
            Assert.Equal(expectation.Claimant, draft.ClaimantName);
            Assert.Equal(expectation.Reference, draft.ClaimNumber);
            Assert.Equal(expectation.Registration, draft.VehicleRegistration);
            Assert.Equal(expectation.Vehicle, draft.VehicleMake);
            Assert.Null(draft.VehicleModel);
            Assert.Equal(expectation.IncidentDate, draft.DateOfIncident);
            Assert.Equal(expectation.InstructionDate, draft.InstructionDate);
            Assert.Equal(expectation.Location, draft.InspectionAddress);
            Assert.Null(draft.InspectionDate);
            Assert.Null(Assert.Single(
                result.Fields,
                field => field.Name == "Report deadline").SuggestedValue);
            Assert.Equal("QC Law", Assert.Single(
                result.Fields,
                field => field.Name == "Document issuer").SuggestedValue);
            Assert.Equal("Complex Reports", Assert.Single(
                result.Fields,
                field => field.Name == "Intermediary").SuggestedValue);
        }
    }

    [ReferencePackTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task OakwoodOriginalsProduceExactAlignedInstructionFields(int sampleIndex)
    {
        var root = PackRoot();
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var policy = new OakInstructionExtractionPolicy();
        var selector = new InstructionExtractionPolicySelector([policy]);
        var expectation = OakExpectations[sampleIndex];

        var absolute = Path.Combine(
            root,
            expectation.PackRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var bytes = await File.ReadAllBytesAsync(absolute);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Assert.Equal(expectation.Sha256, sha256);

        var read = await reader.ReadAsync(
            Source(bytes, Path.GetFileName(absolute), sha256),
            CancellationToken.None);
        Assert.Equal(IntakeSourceReadStatus.Readable, read.Status);
        Assert.False(read.IsIncomplete);
        var selection = selector.Select(read, InstructionDocumentSignature.InstructionRole);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);

        var result = policy.Extract(
            read,
            ProcessedAtUtc,
            new("OAK", policy.DocumentProfileKey, policy.DocumentProfileVersion));
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(expectation.Claimant, draft.ClaimantName);
        Assert.Equal(expectation.Reference, draft.ClaimNumber);
        Assert.Equal(expectation.Registration, draft.VehicleRegistration);
        Assert.Null(draft.VehicleMake);
        Assert.Equal(expectation.Model, draft.VehicleModel);
        Assert.Equal(expectation.IncidentDate, draft.DateOfIncident);
        Assert.Equal(expectation.InstructionDate, draft.InstructionDate);
        Assert.Equal(expectation.InspectionAddress, draft.InspectionAddress);
        Assert.Equal(expectation.Circumstances, draft.AccidentCircumstances);
        Assert.Equal(expectation.Source, Assert.Single(
            result.Fields, field => field.Name == "Source").SuggestedValue);
        Assert.Equal(expectation.Source, Assert.Single(
            result.Fields, field => field.Name == "Introducer").SuggestedValue);
        Assert.Equal(IntakeLocatorKind.TableCell, Assert.Single(
            result.Fields, field => field.Name == "Claim reference").Candidates.Single().Locator!.Kind);
        Assert.Equal(IntakeLocatorKind.TableCell, Assert.Single(
            result.Fields, field => field.Name == "Instruction date").Candidates.Single().Locator!.Kind);
    }

    /// <summary>
    /// The gate each method file proposes: zero WRONG accepted identity. A
    /// value the labeller did not read is a miss and is measured above; a
    /// DIFFERENT value confidently accepted is a failure.
    /// </summary>
    private static IEnumerable<string> WrongIdentity(
        string name,
        SampleExpectation expectation,
        InstructionExtractionResult result)
    {
        var draft = result.InstructionDraft;
        if (draft is null)
        {
            yield return $"{name}: the policy produced no draft at all.";
            yield break;
        }

        foreach (var wrong in new[]
        {
            Compare(name, "claimant", expectation.Identity.ClaimantName, draft.ClaimantName),
            Compare(name, "reference", expectation.Identity.ClaimNumber, draft.ClaimNumber),
            Compare(
                name,
                "registration",
                expectation.Identity.VehicleRegistration,
                draft.VehicleRegistration),
            Compare(
                name,
                "incident date",
                expectation.Identity.DateOfIncident?.ToString("O", CultureInfo.InvariantCulture),
                draft.DateOfIncident?.ToString("O", CultureInfo.InvariantCulture)),
            Compare(
                name,
                "instruction date",
                expectation.Identity.InstructionDate?.ToString("O", CultureInfo.InvariantCulture),
                draft.InstructionDate?.ToString("O", CultureInfo.InvariantCulture))
        })
        {
            if (wrong is not null)
            {
                yield return wrong;
            }
        }
    }

    private static string? Compare(string name, string field, string? expected, string? actual) =>
        expected is null || actual is null || string.Equals(expected, actual, StringComparison.Ordinal)
            ? null
            : $"{name}: the {field} extracted as '{actual}', and the labelled value is '{expected}'.";

    private static IEnumerable<string> NeighbouringValuesThatArrived(
        string name,
        SampleExpectation expectation,
        InstructionExtractionResult result)
    {
        foreach (var negative in expectation.Negatives)
        {
            foreach (var field in result.Fields)
            {
                if (negative.Field is not null
                    && !string.Equals(field.Name, negative.Field, StringComparison.Ordinal))
                {
                    continue;
                }

                if (negative.Field is null && !IdentityFields.Contains(field.Name))
                {
                    continue;
                }

                if (field.SuggestedValue is { } value
                    && value.Contains(negative.Value, StringComparison.OrdinalIgnoreCase))
                {
                    yield return
                        $"{name}: '{field.Name}' carries '{negative.Value}'. {negative.Why}";
                }
            }
        }
    }

    private static readonly HashSet<string> IdentityFields = new(StringComparer.Ordinal)
    {
        "Claimant name",
        "Claim number",
        "Vehicle registration",
        "Vehicle description",
        "Vehicle make",
        "Vehicle model",
        "Date of incident",
        "Instruction date",
        "Accident circumstances"
    };

    /// <summary>
    /// The serialized candidates for one original: field, normalized value,
    /// raw value, party and reference role, document role, source hash,
    /// occurrence, the page, cell, form field or region the reader reported,
    /// the policy key and version, and the disposition.
    /// </summary>
    private static void AppendSample(
        StringBuilder report,
        string name,
        string sha256,
        SampleExpectation expectation,
        InstructionPolicySelection selection,
        InstructionExtractionResult result,
        IInstructionDocumentProfile profile,
        IInstructionExtractionPolicy policy)
    {
        var roles = policy as IInstructionFieldRoles;
        report.AppendLine(CultureInfo.InvariantCulture, $"## {expectation.Profile} — {name}")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"SHA-256 `{sha256}`.")
            .AppendLine(
                CultureInfo.InvariantCulture,
                $"Selected `{profile.DocumentProfileKey}` v{profile.DocumentProfileVersion}; "
                + $"matched template variants: {Variants(selection)}.")
            .AppendLine()
            .AppendLine(
                "| Field | Normalized | Raw | Party role | Reference role | Document role "
                + "| Occurrence | Locator | Policy | Disposition |")
            .AppendLine("| --- | --- | --- | --- | --- | --- | ---: | --- | --- | --- |");

        foreach (var field in result.Fields)
        {
            var role = roles is not null && roles.FieldRoles.TryGetValue(field.Name, out var found)
                ? found
                : new InstructionFieldRole(null, null);
            if (field.Candidates.Count == 0)
            {
                report.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"| {field.Name} | | | {role.PartyRole} | {role.ReferenceRole} "
                    + $"| {profile.Signature.DocumentRole} | 0 | not stated "
                    + $"| {result.PolicyKey} v{result.PolicyVersion} | Missing |");
                continue;
            }

            var disposition = field.HasConflict
                ? nameof(SourceCandidateDisposition.Ambiguous)
                : nameof(SourceCandidateDisposition.Usable);
            var occurrence = 0;
            foreach (var candidate in field.Candidates)
            {
                report.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"| {field.Name} | {Cell(field.HasConflict ? null : field.SuggestedValue)} "
                    + $"| {Cell(candidate.SourceValue)} | {role.PartyRole} | {role.ReferenceRole} "
                    + $"| {profile.Signature.DocumentRole} | {occurrence++} "
                    + $"| {Locator(candidate)} "
                    + $"| {result.PolicyKey} v{result.PolicyVersion} | {disposition} |");
            }
        }

        report.AppendLine();
    }

    /// <summary>
    /// The smallest useful layout locator the reader reported: its page, table
    /// cell, PDF form field, bounded region and message part where it has
    /// them, and the source label it always has.
    /// </summary>
    private static string Locator(InstructionFieldCandidate candidate)
    {
        var parts = new List<string> { Cell(candidate.SourceLabel) };
        var page = candidate.Locator?.Page
            ?? AnalyzeRetainedInstruction.PageFrom(candidate.SourceLabel);
        if (page is { } value)
        {
            parts.Add($"page {value}");
        }

        if (candidate.Locator is { } locator)
        {
            parts.Add($"kind {locator.Kind}");
            if (locator.Cell is { } cell)
            {
                parts.Add($"cell {cell}");
            }

            if (locator.FormField is { } formField)
            {
                parts.Add($"form field {formField}");
            }

            if (locator.Region is { } region)
            {
                parts.Add($"region {region}");
            }

            if (locator.MessagePart != IntakeMessagePart.None)
            {
                parts.Add($"message part {locator.MessagePart}");
            }
        }

        return string.Join("; ", parts);
    }

    private static void Count(
        Dictionary<(string, string, string), int> counts,
        string profile,
        InstructionExtractionResult result)
    {
        foreach (var field in result.Fields)
        {
            var disposition = field.Candidates.Count == 0
                ? nameof(SourceCandidateDisposition.Missing)
                : field.HasConflict
                    ? nameof(SourceCandidateDisposition.Ambiguous)
                    : nameof(SourceCandidateDisposition.Usable);
            var key = (profile, field.Name, disposition);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
    }

    private static void AppendMatrix(
        StringBuilder report,
        Dictionary<(string Profile, string Field, string Disposition), int> counts)
    {
        report.AppendLine("## Measured coverage")
            .AppendLine()
            .AppendLine("| Profile | Field | Usable | Ambiguous | Missing |")
            .AppendLine("| --- | --- | ---: | ---: | ---: |");
        foreach (var group in counts.Keys
            .Select(key => (key.Profile, key.Field))
            .Distinct()
            .OrderBy(key => key.Profile, StringComparer.Ordinal)
            .ThenBy(key => key.Field, StringComparer.Ordinal))
        {
            report.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {group.Profile} | {group.Field} "
                + $"| {counts.GetValueOrDefault((group.Profile, group.Field, nameof(SourceCandidateDisposition.Usable)))} "
                + $"| {counts.GetValueOrDefault((group.Profile, group.Field, nameof(SourceCandidateDisposition.Ambiguous)))} "
                + $"| {counts.GetValueOrDefault((group.Profile, group.Field, nameof(SourceCandidateDisposition.Missing)))} |");
        }
    }

    private static string Describe(InstructionPolicySelection selection) => selection.Outcome switch
    {
        InstructionPolicySelectionOutcome.Selected => selection.Policy!.PrincipalCode,
        InstructionPolicySelectionOutcome.Ambiguous =>
            $"ambiguously {string.Join(", ", selection.Matches.Select(item => item.PrincipalCode))}",
        _ => "no profile"
    };

    private static string Variants(InstructionPolicySelection selection) =>
        selection.MatchedVariantKeys.Count == 0
            ? "none recorded"
            : string.Join(", ", selection.MatchedVariantKeys)
                + (selection.HasAmbiguousVariant ? " (ambiguous)" : string.Empty);

    /// <summary>Table cells: the pipes and newlines a value may carry cannot break the row.</summary>
    private static string Cell(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

    private static IntakeSource Source(byte[] bytes, string name, string sha256) =>
        new(
            name,
            MediaType(name),
            bytes,
            ProcessedAtUtc,
            "top15-instruction-corpus",
            new(IntakeSourceChannel.ManualUpload, $"corpus-{sha256[..12]}"));

    private static string MediaType(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".msg" => "application/vnd.ms-outlook",
            ".eml" => "message/rfc822",
            var other => throw new InvalidOperationException(
                $"The corpus carries an original this test has no media type for: '{other}'.")
        };

    private const string PackRootVariable = PrincipalSourceManifestTests.PackRootVariable;

    private static string PackRoot() =>
        PrincipalSourceManifestTests.ConfiguredPackRoot()
        ?? throw new InvalidOperationException(
            $"{PackRootVariable} is not set; this test should have been skipped.");

    private static void WriteReport(string content)
    {
        var directory = Path.Combine(
            CorpusPackage.RepositoryRoot, "artifacts", "evaluation", "v1-intake");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "top15-instruction-corpus.md"),
            content,
            new UTF8Encoding(false));
    }
}

internal sealed class ReferencePackTheoryAttribute : TheoryAttribute
{
    public ReferencePackTheoryAttribute()
    {
        if (PrincipalSourceManifestTests.ConfiguredPackRoot() is null)
            Skip = $"{PrincipalSourceManifestTests.PackRootVariable} is absent.";
    }
}
