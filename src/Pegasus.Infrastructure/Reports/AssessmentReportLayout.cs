using Pegasus.Core.Reports;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static Pegasus.Core.Reports.AssessmentReportWording;
using static Pegasus.Infrastructure.Reports.ReportChrome;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The printed images one render carries, each decoded once before
/// composition so an undecodable image fails the render closed instead of
/// printing a placeholder: the image page 1 leads with, the images of the
/// image pages in the Engineer's order, the signature and the logo. EXIF
/// orientation, the Engineer's rotation and crop are already applied and
/// each image is already trimmed to the shape of its slot.
/// </summary>
internal sealed record PreparedReportImages(
    byte[]? Lead,
    IReadOnlyList<PreparedReportPhoto> Photos,
    byte[] Signature,
    byte[] Logo);

/// <summary>One decoded photo and whether it prints on a page of its own (v28 P41).</summary>
internal sealed record PreparedReportPhoto(byte[] Content, bool FullPage);

/// <summary>
/// The one assessment-report layout (FRD-11, ADR-0050): the report, the image
/// pack and the fee note composed as QuestPDF pages from an accepted
/// <see cref="AssessmentReportSnapshot"/>, to the template's measurements
/// (<c>reference/rendererref1</c>). Composition is pure and decides geometry
/// alone: every word it prints is Core's
/// (<see cref="AssessmentReportWording"/>, the snapshot's own narrative and
/// statement of truth), so it holds no printed text of its own.
/// </summary>
internal static class AssessmentReportLayout
{
    /// <summary>
    /// Exactly the requested artifact kind. An assessment report frozen with
    /// <see cref="AssessmentReportSnapshot.IncludeFeeNote"/> ends with the fee
    /// note's own pages: the same fee facts and the same accepted terms the
    /// separate document prints, after a page break, in one document.
    /// </summary>
    internal static Document Compose(
        AssessmentReportSnapshot snapshot,
        CaseReportArtifactKind kind,
        PreparedReportImages images)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(images);
        if (kind is not (CaseReportArtifactKind.AssessmentReport
            or CaseReportArtifactKind.ImagePack
            or CaseReportArtifactKind.FeeNote))
        {
            throw new ReportRenderRejectedException($"Unsupported report artifact kind '{kind}'.");
        }
        if (kind == CaseReportArtifactKind.ImagePack && images.Photos.Count == 0)
        {
            throw new ReportRenderRejectedException(
                "An image pack needs at least one image the report uses.");
        }
        return Document.Create(container =>
        {
            void Pages(bool feeNote, Action<ColumnDescriptor> body) => container.Page(page => Page(
                page,
                images.Logo,
                CompanyBlock(feeNote),
                Footer(snapshot, feeNote),
                feeNote ? FeeNoteBodyTop : ReportBodyTop,
                body));

            switch (kind)
            {
                case CaseReportArtifactKind.FeeNote:
                    Pages(feeNote: true, column => FeeNote(column, snapshot));
                    break;
                case CaseReportArtifactKind.ImagePack:
                    Pages(feeNote: false, column => ImagePack(column, snapshot, images));
                    break;
                default:
                    Pages(feeNote: false, column => Report(column, snapshot, images));
                    if (snapshot.IncludeFeeNote)
                    {
                        Pages(feeNote: true, column => FeeNote(column, snapshot));
                    }
                    break;
            }
        });
    }

    // ---- Image pack (v28 P22) ----------------------------------------------

    /// <summary>
    /// The report's images alone, in the Engineer's order, on the report's
    /// own image pages: six to a page and a Full page image on a page of its
    /// own. It opens as the report opens, so the document says which Case it
    /// belongs to, and carries no narrative, no figures and no statement of
    /// truth.
    /// </summary>
    private static void ImagePack(
        ColumnDescriptor column, AssessmentReportSnapshot snapshot, PreparedReportImages images)
    {
        Title(column, ImagePackTitle, italic: true, ReportTitle);
        AddresseeBand(column, ReportTitle.Beneath, ReportForLabel, snapshot.ReportFor, References(snapshot));
        Images(column, images.Photos, gapAbove: SlotGap);
    }

    // ---- Assessment report -------------------------------------------------

    private static void Report(ColumnDescriptor column, AssessmentReportSnapshot snapshot, PreparedReportImages images)
    {
        var presentation = snapshot.Presentation();

        // Page 1: the outcome at a glance, the vehicle, and the two slots.
        Title(column, presentation.Title, italic: true, ReportTitle);
        AddresseeBand(column, ReportTitle.Beneath, ReportForLabel, snapshot.ReportFor, References(snapshot));
        LabelledLine(column, MatterGap, MatterLabel, Matter(snapshot));
        column.Item().PaddingTop(AfterMatterGap, Unit.Millimetre).Row(row =>
        {
            row.Spacing(3, Unit.Millimetre);
            Badge(row.AutoItem(), presentation.Badge, Charcoal);
            Badge(row.AutoItem(), RoadworthinessBadge(snapshot), snapshot.IsUnroadworthy ? Brand : Charcoal);
        });
        Tiles(column.Item().PaddingTop(4, Unit.Millimetre), AssessmentReportWording.Tiles(snapshot));
        Paragraph(column, Introduction(snapshot), gapAbove: 5);
        Section(column, VehicleDetailsHeading, ReportRhythm, opensPage: false, Brand, section =>
            LabelGrid(section.Item(), TableSize, (20, 30), (20, 30), VehicleDetails(snapshot)));
        var plan = DamagePlanDrawing.Svg(snapshot.Damage.Impacts);
        Gap(column, SlotGap);
        column.Item()
            .ShowEntire()
            .Element(slots => ImageSlots(
                slots,
                LeadSlotHeight,
                images.Lead is null ? null : slot => Fill(slot, images.Lead),
                slot => slot.AlignCenter().Svg(plan).FitHeight()));

        // Page 2: the narrative, in the Engineer's order (v28 P30). The
        // settlement block keeps the value box that belongs to it.
        var narrative = new List<(string Title, Action<ColumnDescriptor> Body)>();
        if (snapshot.IsImageBased)
        {
            narrative.Add((DesktopAssessmentHeading, section => Paragraphs(section, DesktopAssessment)));
        }
        foreach (var block in snapshot.PrintedWording)
        {
            var printed = block;
            narrative.Add((printed.Title, section =>
            {
                Paragraphs(section, printed.Text);
                if (printed.Key == ReportWordingComposition.Settlement)
                {
                    Gap(section, 4);
                    ReportChrome.ValueBox(
                        section.Item(),
                        ValueBoxLabelCells(snapshot),
                        AssessmentReportWording.ValueBox(snapshot).Value);
                }
            }));
        }
        for (var index = 0; index < narrative.Count; index++)
        {
            if (index == 0)
            {
                column.Item().PageBreak();
            }
            Section(column, narrative[index].Title, ReportRhythm, opensPage: index == 0, Brand, narrative[index].Body);
        }

        // Page 3: the vehicle's data and the repair cost.
        column.Item().PageBreak();
        FactTable(column.Item(), VehicleDataHeading, VehicleData(snapshot));
        Gap(column, TableGap);
        MoneyTable(
            column.Item().ShowEntire(),
            124.32f,
            27.35f,
            RepairCostHeading,
            AmountHeading,
            RepairCosts(snapshot.Costs));

        // Page 4: the work lists that hold anything.
        var lists = WorkLists(snapshot);
        for (var index = 0; index < lists.Count; index++)
        {
            if (index == 0)
            {
                column.Item().PageBreak();
            }
            else
            {
                Gap(column, TableGap);
            }
            TwoColumnList(column.Item(), lists[index].Title, lists[index].Items);
        }

        // The image pages. The image page 1 leads with is not repeated here.
        if (images.Photos.Count > 0)
        {
            column.Item().PageBreak();
            Section(column, VehicleImagesHeading, ReportRhythm, opensPage: true, Brand, _ => { });
            Images(column, images.Photos, gapAbove: 0.18f);
        }

        column.Item().PageBreak();
        Section(column, StatementOfTruthHeading, ReportRhythm, opensPage: true, Brand, section =>
        {
            var statement = StatementOfTruthParagraphs(snapshot);
            for (var index = 0; index < statement.Count; index++)
            {
                Paragraph(section, statement[index], index == 0 ? 0 : ParagraphGap);
            }
        });
        SignOff(column, snapshot.Signatory, images.Signature);
    }

    /// <summary>
    /// The valediction, the signature in its 52 by 27.4 mm slot, and who
    /// signed. It is never parted over two pages.
    /// </summary>
    private static void SignOff(ColumnDescriptor column, ReportSignatory signatory, byte[] signature)
    {
        Gap(column, ParagraphGap);
        column.Item()
            .ShowEntire()
            .Column(block =>
            {
                block.Item().Text(Valediction);
                block.Item()
                    .PaddingTop(8, Unit.Millimetre)
                    .Width(52, Unit.Millimetre)
                    .Height(27.4f, Unit.Millimetre)
                    .AlignLeft()
                    .AlignBottom()
                    .Image(signature)
                    .UseOriginalImage()
                    .FitArea();
                block.Item().PaddingTop(0.95f, Unit.Millimetre).Text(SignatoryLine(signatory)).Bold();
                block.Item().Text(SignatoryRole).FontSize(DetailSize).FontColor(Muted);
                block.Item().Text(CompanyEmail).FontSize(DetailSize).FontColor(Muted);
            });
    }

    // ---- Fee note ----------------------------------------------------------

    private static void FeeNote(ColumnDescriptor column, AssessmentReportSnapshot snapshot)
    {
        Title(column, AssessmentReportWording.FeeNoteTitle, italic: false, ReportChrome.FeeNoteTitle);
        AddresseeBand(
            column, ReportChrome.FeeNoteTitle.Beneath, BillToLabel, BillTo(snapshot.ReportFor), References(snapshot));
        LabelledLine(column, MatterGap, MatterLabel, Matter(snapshot));
        Gap(column, AfterMatterGap);
        MoneyTable(
            column.Item(),
            null,
            36.5f,
            FeeDescriptionHeading,
            FeeAmountHeading,
            FeeTotals(snapshot),
            table => DescribedFigure(table, FeeTitle(snapshot), snapshot.FeeDescriptionLines, FeeAmount(snapshot)));
        Section(column, PaymentDetailsHeading, FeeNoteRhythm, opensPage: false, Brand, section =>
            LabelGrid(section.Item(), GridSize, (31.42f, 46.5f), (31.68f, 55.9f), PaymentDetails(snapshot)));
        Section(column, TermsHeading, FeeNoteRhythm, opensPage: false, Ink, section =>
        {
            for (var index = 0; index < Terms.Count; index++)
            {
                Gap(section, index == 0 ? 0 : 1.8f);
                section.Item()
                    .Text(Terms[index])
                    .FontSize(7.5f)
                    .FontColor(Muted)
                    .LineHeight(1.35f)
                    .Justify();
            }
        });
        Gap(column, 3);
        column.Item()
            .AlignCenter()
            .Text(ThankYou)
            .Italic()
            .FontColor(Muted);
    }
}
