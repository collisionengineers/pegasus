using System.Numerics;

namespace Pegasus.Core.Intake;

/// <summary>
/// One recurring graphic that is never a photograph: its reference
/// fingerprint, how many bits a copy may differ by, and the exact size it is
/// always sent at, when it has one.
/// </summary>
public sealed record KnownGraphic(
    string Name,
    ulong Fingerprint,
    int MaximumDistance,
    int? WidthPixels = null,
    int? HeightPixels = null)
{
    public bool Matches(IntakeAssetCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.Fingerprint is { } fingerprint
            && (WidthPixels is null || candidate.WidthPixels == WidthPixels)
            && (HeightPixels is null || candidate.HeightPixels == HeightPixels)
            && BitOperations.PopCount(fingerprint ^ Fingerprint) <= MaximumDistance;
    }
}

/// <summary>
/// The graphics that pass the photograph size rule but recur across the
/// corpus, named so the photograph rule (<see cref="InstructionEvidenceImages"/>)
/// leaves them out (operator, 26 October 2026). Each one recurs in three or
/// more corpus documents, except the bar chart with the right bar the
/// taller, a second layout of a listed chart (R540). An exact byte hash cannot name them, because every
/// sender re-encodes them; the corpus holds 33 byte-different copies of the
/// Collision Engineers logo alone. A difference-hash fingerprint survives
/// re-encoding and resizing.
///
/// The fingerprint also groups copies of one photograph forwarded or resized
/// across e-mails, so every entry is held to a tight distance and, where the
/// graphic is always sent at one size, to that exact size. Each distance is
/// the entry's spread (how far its corpus copies sit from the reference) plus
/// four bits for a new encoding, and stays below the nearest corpus image that
/// is not a copy: of any size for an entry that matches any size, of the
/// entry's own size otherwise. Measured on 27 October 2026 with the reader's
/// own fingerprint over 18,440 corpus images (artifacts/qdos26120/skia). A
/// new entry needs the operator's approval and the same measurement.
/// </summary>
public static class KnownGraphics
{
    public static IReadOnlyList<KnownGraphic> Entries { get; } =
    [
        // 402 documents, 33 encodings at sizes from 140x78 to 3150x1756, white
        // and black backgrounds; spread 7, nearest photograph 15.
        new("Collision Engineers logo", 0xc0a02a2b4c24a0c0UL, 11),
        // 80 documents, e.g. "BL19XPO.pdf"; nearest other image 15.
        new("IAEA seal", 0x4ce8cc8ed4ecaa70UL, 4, 200, 245),
        // 56 documents, the QDOS instruction letters; spread 5, nearest other 17.
        new("MGAA logo", 0xa1e2ca2d4149d964UL, 9, 204, 204),
        // 13 e-mails, an inline signature; nearest other 15.
        new("MotorAssist signature banner", 0x40aab6b41b805800UL, 4, 600, 205),
        // 14 documents, including a.QDOS26120's "1_Images-V1.pdf"; nearest other 16.
        new("LAIRD logo", 0x00192455db260000UL, 4, 320, 214),
        // 14 Bodyshop reports, 13 encodings; spread 10. The reports' 300x300 bar
        // chart is the nearest image of that size, at 32.
        new("Bodyshop pie chart", 0x1f1b1d32f2870f0fUL, 14, 300, 300),
        // The same reports' Repair Costs Analysis chart: two bars whose heights
        // follow the report's figures (operator, 28 October 2026, R476, from
        // a.QDOS26123's "Bodyshopreport316478-V1.pdf"). Measured on that one
        // report with the reader's own fingerprint; the layout's bar-height
        // variants with the left bar the taller sit within 12 of it and no
        // nearer than 37 to the pie chart, so the two entries never share an
        // image. Over the corpus (issue 1718) the chart is 13 encodings in 14
        // documents: the 12 with the left bar the taller sit within 8, and the
        // one with the right bar the taller sits at 19, outside this entry and
        // listed next. That report's chart is the fixture KnownGraphicsTests
        // reads.
        new("Bodyshop bar chart", 0xc0c6e6e6e6e6e6e6UL, 16, 300, 300),
        // The same chart with the right bar the taller, from the one corpus
        // copy, "1_Bodyshopreport-V1.pdf" (operator, R540). It sits 19 from the
        // entry above and 32 from the pie chart, so 2 is the largest distance
        // that cannot share an image with the entry above (19 > 16 + 2).
        new("Bodyshop bar chart, right bar taller", 0x0c0e0e0e0ee6e6e6UL, 2, 300, 300),
        // 14 Bodyshop reports, 13 encodings; spread 16. No other corpus image
        // is 450x282.
        new("Bodyshop damage diagram", 0x00f0ccccccccf000UL, 20, 450, 282),
        // 8 QDOS instruction e-mails. No other corpus image is 696x564.
        new("Engineer signature", 0x0000e0c00c000000UL, 4, 696, 564),
        // 19 engineer reports, e.g. PCH's "Report 00077570.pdf" and "CNX 01.pdf";
        // spread 0, nearest other image 9; no other corpus image is 744x405
        // (operator, R540).
        new("PCH engineer signature", 0x000000c8a8b02000UL, 4, 744, 405),
        // 5 instruction letters, "KMR 01.pdf" to "KMR 05.pdf"; spread 0, nearest
        // other image 6; no other corpus image is 501x501 (operator, R540).
        new("KMR Law logo", 0x0000d4d4d4d40000UL, 4, 501, 501),
        // Mirrored text, in two sizes 11 bits apart, so each has its own sized
        // entry. 15 engineer reports, e.g. "BV64HYJ.pdf"; spread 0, nearest
        // other image 5; no other corpus image is 376x234 (operator, R540).
        new("Mirrored text strip 376x234", 0x00000000000b9adcUL, 4, 376, 234),
        // 10 reports and fee notes, e.g. "BV64HYJ.pdf" and "DK66LRL fee.pdf";
        // spread 0, nearest other image 5; no other corpus image is 360x243
        // (operator, R540).
        new("Mirrored text strip 360x243", 0x000000000095aef9UL, 4, 360, 243),
        // 4 e-mails; nearest other 16.
        new("Text disclaimer image", 0xc0b4ca9e943aa2b0UL, 4, 882, 369)
    ];

    /// <summary>Whether a candidate image is one of the named graphics.</summary>
    public static bool Contains(IntakeAssetCandidate candidate) =>
        Entries.Any(entry => entry.Matches(candidate));
}
