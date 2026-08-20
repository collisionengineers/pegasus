using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ENG-002: the one synthetic Audatex-shaped fixture, shared by the parser
/// tests and the assessment-page import tests. Built in-test with PdfPig's
/// writer; no real estimate is committed.
/// </summary>
internal static class AudatexEstimateFixture
{
    /// <summary>
    /// A synthetic Audatex-shaped report: labour and paint tables whose work
    /// units print one point below their description rows, a parts table with
    /// a priced and an unpriced row, an extras table with inline prices, and
    /// the document's own section totals.
    /// </summary>
    internal static byte[] Build(
        string partsSubTotal = "£620.20",
        string labourTotalWorkUnits = "21.0",
        bool extraOrphanAmount = false,
        bool includeIdentity = true)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        void Text(double x, double y, string text) =>
            page.AddText(text, 9, new PdfPoint(x, y), font);

        if (includeIdentity)
        {
            Text(20, 700, "Assessment Number:");
            Text(167, 700, "TEST01");
            Text(20, 688, "Version:");
            Text(80, 688, "V1/1");
        }

        // LABOUR: guide + description rows with the work-unit value on its
        // own baseline 1pt below, a continuation row, and the printed total.
        Text(20, 660, "LABOUR");
        Text(20, 648, "Number");
        Text(159, 648, "Description");
        Text(485, 648, "Work");
        Text(518, 648, "Units");
        Text(20, 636, "12 34 567");
        Text(159, 636, "R + R FRONT BUMPER");
        Text(522, 635, "9.0");
        Text(20, 624, "0742");
        Text(159, 624, "REPAIR WING");
        Text(522, 623, "12.0");
        if (extraOrphanAmount)
        {
            Text(522, 621.5, "3.0");
        }
        Text(159, 612, "(TRIM REMOVED)");
        Text(259, 600, "Total");
        Text(291, 600, "Work");
        Text(324, 600, "Units");
        Text(515, 600, labourTotalWorkUnits);

        Text(20, 580, "PAINT WORK");
        Text(20, 568, "Number");
        Text(159, 568, "Description");
        Text(485, 568, "Work");
        Text(518, 568, "Units");
        Text(20, 556, "283");
        Text(159, 556, "FRONT BUMPER NEW PART PAINT");
        Text(522, 555, "16.2");
        Text(259, 544, "Total");
        Text(291, 544, "Work");
        Text(324, 544, "Units");
        Text(515, 544, "16.2");

        Text(20, 520, "PARTS");
        Text(20, 508, "Guide");
        Text(56, 508, "No.");
        Text(103, 508, "Description");
        Text(242, 508, "Part");
        Text(269, 508, "Number");
        Text(353, 508, "Bet.");
        Text(519, 508, "Price");
        Text(20, 496, "283");
        Text(103, 496, "FRONT BUMPER");
        Text(242, 496, "51 11 8 067");
        Text(353, 496, "0%");
        Text(501, 495, "£620.20");
        Text(20, 484, "431");
        Text(103, 484, "GRILLE BADGE");
        Text(242, 484, "51 76 7");
        Text(353, 484, "0%");
        Text(214, 472, "Sub");
        Text(239, 472, "Total");
        Text(490, 472, partsSubTotal);

        Text(20, 450, "Extras");
        Text(103, 438, "Description");
        Text(325, 438, "Betterment");
        Text(519, 438, "Price");
        Text(103, 426, "4 WHEEL ALIGNMENT");
        Text(214, 426, "Specialist");
        Text(325, 426, "0%");
        Text(510, 426, "£110.00");
        Text(325, 414, "Total");
        Text(357, 414, "Extras");
        Text(501, 414, "£110.00");

        Text(20, 22, "Audatex System Using Manufacturer Times");

        return builder.Build();
    }
}
