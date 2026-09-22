using System.Globalization;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Reports;
using QuestPDF.Elements.Table;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Pegasus.Infrastructure.Reports.ReportChrome;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The printed images one render carries: every report photo as the square
/// the page prints (EXIF orientation, the Engineer's rotation and crop
/// already applied, in snapshot order) plus the signature, all decoded
/// once before composition so an undecodable image fails the render closed
/// instead of printing a placeholder.
/// </summary>
internal sealed record PreparedReportImages(
    IReadOnlyList<PreparedReportPhoto> Photos,
    byte[] Signature,
    byte[] Logo);

/// <summary>One decoded photo and whether it prints on a page of its own (v28 P41).</summary>
internal sealed record PreparedReportPhoto(byte[] Content, bool FullPage);

/// <summary>
/// The one assessment-report layout (FRD-11, ADR-0050): the report and the
/// fee note composed as QuestPDF pages from an accepted
/// <see cref="AssessmentReportSnapshot"/>. Composition is pure — it draws
/// only what the snapshot supplies, in the section order, registers, tables
/// and rules the accepted template carried, and fails closed on an
/// unsupported value.
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
        var feeNote = kind switch
        {
            CaseReportArtifactKind.FeeNote => true,
            CaseReportArtifactKind.AssessmentReport or CaseReportArtifactKind.ImagePack => false,
            _ => throw new ReportRenderRejectedException($"Unsupported report artifact kind '{kind}'."),
        };
        var imagePack = kind == CaseReportArtifactKind.ImagePack;
        if (imagePack && images.Photos.Count == 0)
        {
            throw new ReportRenderRejectedException(
                "An image pack needs at least one image the report uses.");
        }
        return Document.Create(container =>
        {
            void AddPages(bool pageIsFeeNote)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    // The accepted print margins: 8mm top, 12mm each side and 22mm at
                    // the foot, of which the footer band takes the upper 14mm so the
                    // page numbering sits inside the margin, as the browser footer did.
                    page.MarginTop(8, Unit.Millimetre);
                    page.MarginHorizontal(12, Unit.Millimetre);
                    page.MarginBottom(8, Unit.Millimetre);
                    page.DefaultTextStyle(style => style
                        .FontFamily(FontFamily)
                        .FontSize(DataRegister)
                        .FontColor(Ink)
                        .LineHeight(BodyLineHeight));
                    page.Content().Column(column =>
                    {
                        if (pageIsFeeNote)
                        {
                            FeeNote(column, snapshot, images.Logo);
                        }
                        else if (imagePack)
                        {
                            ImagePack(column, snapshot, images);
                        }
                        else
                        {
                            Report(column, snapshot, images);
                        }
                    });
                    page.Footer()
                        .Height(14, Unit.Millimetre)
                        .AlignBottom()
                        .Element(footer => Footer(footer, snapshot, pageIsFeeNote));
                });
            }

            AddPages(feeNote);
            if (kind == CaseReportArtifactKind.AssessmentReport && snapshot.IncludeFeeNote)
            {
                AddPages(pageIsFeeNote: true);
            }
        });
    }

    // ---- Image pack (v28 P22) ----------------------------------------------

    /// <summary>
    /// The included images alone, in the Engineer's order, two ordinary images
    /// per page with a Full page image on a page of its own, under the report's
    /// own letterhead so the document says which Case it belongs to. It carries
    /// no narrative, no figures and no statement of truth: it is the report's
    /// images, sent beside it.
    /// </summary>
    private static void ImagePack(
        ColumnDescriptor column, AssessmentReportSnapshot snapshot, PreparedReportImages images)
    {
        Letterhead(column, images.Logo, references => references.Column(lines =>
        {
            Reference(lines, "Date:", Date(snapshot.ReportDate));
            Reference(lines, "Our Ref:", snapshot.OurReference);
            Reference(lines, "Your Ref:", snapshot.YourReference);
        }));

        Title(column, "Vehicle Images", italic: true);
        Section(column, "Vehicle Images", section => ImagePackPhotos(section.Item(), images.Photos));
    }

    // ---- Assessment report -------------------------------------------------

    private static void Report(ColumnDescriptor column, AssessmentReportSnapshot snapshot, PreparedReportImages images)
    {
        var presentation = snapshot.Presentation();

        Letterhead(column, images.Logo, references => references.Column(lines =>
        {
            Reference(lines, "Date:", Date(snapshot.ReportDate));
            Reference(lines, "Our Ref:", snapshot.OurReference);
            Reference(lines, "Your Ref:", snapshot.YourReference);
        }));

        Title(column, presentation.Title, italic: true);
        column.Item().PaddingBottom(3, Unit.Millimetre).Text(text =>
        {
            text.DefaultTextStyle(style => style.LineHeight(1.35f));
            text.Line("Report For:").Bold();
            foreach (var addressee in snapshot.ReportFor)
            {
                text.Line(addressee);
            }
        });
        Paragraph(column, text =>
        {
            text.Span("Matter:").Bold();
            text.Span($" Road Traffic Accident: {snapshot.ClaimantName}: {Date(snapshot.IncidentDate)}");
        });

        column.Item().PaddingVertical(3, Unit.Millimetre).Row(row =>
        {
            row.Spacing(3, Unit.Millimetre);
            Badge(row.AutoItem(), presentation.Badge, Charcoal);
            Badge(row.AutoItem(), snapshot.LegalStatus.ToUpperInvariant(), Brand);
        });
        column.Item().PaddingTop(3, Unit.Millimetre).PaddingBottom(5, Unit.Millimetre).Row(row =>
        {
            row.Spacing(2, Unit.Millimetre);
            var tiles = Tiles(snapshot, presentation);
            foreach (var tile in tiles)
            {
                Tile(row.RelativeItem(), tile.Label, tile.Value, tile.Highlight);
            }
            // The accepted layout is a four-column grid whether three or four
            // tiles are printed; the unused column stays empty.
            for (var i = tiles.Length; i < 4; i++)
            {
                row.RelativeItem();
            }
        });

        Paragraph(column, Introduction(snapshot));

        Section(column, "Vehicle Details", section =>
            DataTable(section.Item(), 50, VehicleRows(snapshot)));

        if (snapshot.AssessmentMethod == "image_based")
        {
            Section(column, "Desktop Assessment", section => Paragraph(
                section,
                "This report has been compiled from a desktop review of the information available relating to this claim."));
        }

        // v28 P30: the report's narrative is the Engineer's wording blocks in
        // their order. The settlement block keeps the figure box and the
        // settlement rows that belong to it; every other block is its heading
        // and its paragraphs. The damage tables are not wording: they follow
        // the Nature of Incident block wherever the Engineer put it, and print
        // after the narrative when that block is not on the report.
        var damageTablesPrinted = false;
        void DamageTables()
        {
            damageTablesPrinted = true;
            Section(column, "Damage", section =>
            {
                ImpactDiagram(section, snapshot.Damage.Impacts);
                ImpactTable(section.Item(), snapshot.Damage.Impacts);
                DataTable(section.Item(), 46, DamageRows(snapshot));
            });

            Section(column, "Tyres and Seat Belts", section =>
                DataTable(section.Item(), 40, RestraintRows(snapshot.Damage)));
        }

        foreach (var block in snapshot.PrintedWording)
        {
            var printed = block;
            Section(column, printed.Title, section =>
            {
                foreach (var paragraph in printed.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
                {
                    Paragraph(section, paragraph.Trim());
                }
                if (printed.Key != ReportWordingComposition.Settlement)
                {
                    return;
                }
                ValueBox(section.Item(), presentation.SettlementLabel, Money(presentation.RecommendedSettlement!.Value));
                DataTable(section.Item(), 46, SettlementRows(snapshot.Settlement));
            });
            if (printed.Key == ReportWordingComposition.NatureOfIncident)
            {
                DamageTables();
            }
        }
        if (!damageTablesPrinted)
        {
            DamageTables();
        }

        column.Item().PageBreak();
        Section(column, "Vehicle Data", section =>
            DataTable(section.Item(), 32, VehicleDataRows(snapshot)));
        Section(column, "Repair Cost Calculation", section => CostTable(section.Item(), snapshot.Costs));

        var worklists = new (string Title, IReadOnlyList<string> Items)[]
        {
            ("Main New Parts Required", snapshot.NewParts),
            ("Repairs Required", snapshot.Repairs),
            ("Additional Operations", snapshot.Operations),
        }.Where(list => list.Items.Count > 0).ToArray();
        if (worklists.Length > 0)
        {
            column.Item().PageBreak();
            foreach (var (title, items) in worklists)
            {
                Section(column, title, section => WorkList(section.Item(), items), bottomGap: 4);
            }
        }

        column.Item().PageBreak();
        Section(column, "Vehicle Images", section => PhotoGrid(section.Item(), images.Photos));

        column.Item().PageBreak();
        Section(column, "Statement of Truth", section =>
        {
            foreach (var paragraph in StatementOfTruth(snapshot))
            {
                Paragraph(section, paragraph);
            }
        });
        SignatureBlock(column, snapshot.Signatory, images.Signature);
    }

    private static void SignatureBlock(ColumnDescriptor column, ReportSignatory signatory, byte[] signature)
    {
        column.Item()
            .PaddingTop(6, Unit.Millimetre)
            .PaddingBottom(2, Unit.Millimetre)
            .ShowEntire()
            .Column(block =>
            {
                block.Item().PaddingBottom(2, Unit.Millimetre).Text("Yours faithfully,").FontSize(LetterRegister);
                block.Item()
                    .PaddingVertical(1, Unit.Millimetre)
                    .AlignLeft()
                    .Height(14, Unit.Millimetre)
                    .MaxWidth(60, Unit.Millimetre)
                    .Image(signature)
                    .UseOriginalImage()
                    .FitArea();
                var name = string.IsNullOrWhiteSpace(signatory.Qualifications)
                    ? signatory.PrintedName
                    : $"{signatory.PrintedName} — {signatory.Qualifications}";
                block.Item().Text(name).FontSize(LetterRegister).Bold();
                block.Item()
                    .PaddingTop(0.5f, Unit.Millimetre)
                    .Text($"Independent Automotive Engineer, {CompanyName}")
                    .FontSize(9)
                    .FontColor(Muted);
                block.Item().PaddingTop(ParagraphGap, Unit.Millimetre).Text(CompanyEmail);
            });
    }

    // ---- Fee note ----------------------------------------------------------

    private static void FeeNote(ColumnDescriptor column, AssessmentReportSnapshot snapshot, byte[] logo)
    {
        Letterhead(column, logo, company => company.Text(text =>
        {
            text.Line(CompanyName).Bold();
            text.Line("Independent Automotive Experts");
            text.Line($"VAT No: {AssessmentReportContract.VatNumber}");
            text.Line(CompanyEmail);
            text.Span(CompanyWebsite);
        }));

        Title(column, "FEE NOTE", italic: false);

        column.Item()
            .PaddingVertical(4, Unit.Millimetre)
            .DefaultTextStyle(style => style.FontSize(9))
            .Row(row =>
            {
                row.Spacing(6, Unit.Millimetre);
                row.RelativeItem().Column(billTo =>
                {
                    billTo.Item()
                        .PaddingBottom(1, Unit.Millimetre)
                        .Text("BILL TO:")
                        .FontSize(7.5f)
                        .Bold()
                        .FontColor(Muted)
                        .LetterSpacing(0.06f);
                    billTo.Item().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.LineHeight(1.5f));
                        foreach (var addressee in snapshot.ReportFor)
                        {
                            text.Line(addressee);
                        }
                    });
                });
                row.RelativeItem().Column(facts =>
                {
                    facts.Spacing(1, Unit.Millimetre);
                    KeyValue(facts, 16, "Date:", Date(snapshot.ReportDate));
                    KeyValue(facts, 16, "Our Ref:", snapshot.OurReference);
                    KeyValue(facts, 16, "Your Ref:", snapshot.YourReference);
                    KeyValue(facts, 16, "Matter:", $"Road Traffic Accident: {snapshot.ClaimantName}: {Date(snapshot.IncidentDate)}");
                });
            });

        var descriptions = snapshot.FeeDescriptionLines.Count == 0
            ? ["Independent automotive engineering assessment"]
            : snapshot.FeeDescriptionLines;
        column.Item()
            .PaddingTop(3, Unit.Millimetre)
            .DefaultTextStyle(style => style.FontSize(9))
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(30, Unit.Millimetre);
                });
                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Description", 8.5f, verticalPadding: 1.8f, horizontalPadding: 2);
                    HeaderCell(header.Cell(), "Amount (£)", 8.5f, alignRight: true, verticalPadding: 1.8f, horizontalPadding: 2);
                });
                table.Cell()
                    .BorderBottom(0.4f)
                    .BorderColor(Rule)
                    .Padding(2, Unit.Millimetre)
                    .Text(text =>
                    {
                        text.DefaultTextStyle(style => style.LineHeight(1.4f));
                        text.Line($"Vehicle Damage Assessment Report — {snapshot.Vehicle.Registration}").Bold();
                        for (var i = 0; i < descriptions.Count; i++)
                        {
                            var line = descriptions[i];
                            if (i < descriptions.Count - 1)
                            {
                                text.Line(line).FontSize(8).FontColor(Muted);
                            }
                            else
                            {
                                text.Span(line).FontSize(8).FontColor(Muted);
                            }
                        }
                    });
                table.Cell()
                    .BorderBottom(0.4f)
                    .BorderColor(Rule)
                    .Padding(2, Unit.Millimetre)
                    .AlignRight()
                    .Text(Number(snapshot.FeeNet))
                    .LineHeight(1.4f);
            });

        column.Item()
            .PaddingTop(1, Unit.Millimetre)
            .AlignRight()
            .Width(70, Unit.Millimetre)
            .DefaultTextStyle(style => style.FontSize(9))
            .Column(totals =>
            {
                TotalRow(totals.Item().PaddingVertical(1.2f, Unit.Millimetre), "Subtotal (Net)", Number(snapshot.FeeNet));
                var vatPercent = (AssessmentReportContract.FeeVatRate * 100m)
                    .ToString("0.##", CultureInfo.InvariantCulture);
                TotalRow(totals.Item().PaddingVertical(1.2f, Unit.Millimetre), $"VAT @ {vatPercent}%", Number(snapshot.FeeVat));
                TotalRow(
                    totals.Item()
                        .PaddingTop(1, Unit.Millimetre)
                        .BorderTop(1.2f)
                        .BorderColor(Brand)
                        .Background(Shade)
                        .Padding(2, Unit.Millimetre)
                        .DefaultTextStyle(style => style.FontSize(10.5f).Bold()),
                    "TOTAL DUE",
                    Money(snapshot.FeeTotal));
            });

        Section(column, "Payment Details", section => section.Item()
            .PaddingTop(1, Unit.Millimetre)
            .DefaultTextStyle(style => style.FontSize(9))
            .Column(grid =>
            {
                grid.Spacing(1, Unit.Millimetre);
                KeyValue(grid, 34, "Account Name", AssessmentReportContract.AccountName);
                KeyValue(grid, 34, "Bank", AssessmentReportContract.BankName);
                KeyValue(grid, 34, "Sort Code", AssessmentReportContract.SortCode);
                KeyValue(grid, 34, "Account Number", AssessmentReportContract.AccountNumber);
                KeyValue(grid, 34, "Payment Reference", snapshot.OurReference);
                KeyValue(grid, 34, "Remittance Email", AssessmentReportContract.RemittanceEmail);
            }));

        Section(column, "Terms", section =>
        {
            section.Item().PaddingTop(3, Unit.Millimetre).Text(AssessmentReportContract.FeeTerms).FontSize(8.5f).FontColor(Muted);
            section.Item().PaddingTop(3, Unit.Millimetre).Text(AssessmentReportContract.AdditionalFeeTerms).FontSize(8.5f).FontColor(Muted);
        });
        column.Item().PaddingTop(ParagraphGap, Unit.Millimetre).Text("Thank you for your business.").Bold();
    }

    private static void TotalRow(IContainer container, string label, string value) => container.Row(row =>
    {
        row.RelativeItem().Text(label);
        row.AutoItem().Text(value);
    });

    private static void KeyValue(ColumnDescriptor column, float keyWidthMillimetres, string key, string value) =>
        column.Item().Row(row =>
        {
            row.Spacing(4, Unit.Millimetre);
            row.ConstantItem(keyWidthMillimetres, Unit.Millimetre).Text(key).Bold();
            row.RelativeItem().Text(value);
        });

    // ---- Shared building blocks -------------------------------------------

    private static void Paragraph(ColumnDescriptor column, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        column.Item().PaddingVertical(ParagraphGap / 2, Unit.Millimetre).Text(text);
    }

    private static void Paragraph(ColumnDescriptor column, Action<TextDescriptor> text) =>
        column.Item().PaddingVertical(ParagraphGap / 2, Unit.Millimetre).Text(text);

    private static void ImpactTable(IContainer container, IReadOnlyList<ReportImpact> impacts) => container.Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(40, Unit.Millimetre);
            columns.ConstantColumn(30, Unit.Millimetre);
            columns.RelativeColumn();
        });
        table.Header(header =>
        {
            HeaderCell(header.Cell(), "Areas");
            HeaderCell(header.Cell(), "Severity");
            HeaderCell(header.Cell(), "Note");
        });
        if (impacts.Count == 0)
        {
            BodyCell(table.Cell().ColumnSpan(3), even: false).Text("—");
            return;
        }
        for (var i = 0; i < impacts.Count; i++)
        {
            var even = i % 2 == 1;
            BodyCell(table.Cell(), even).Text(impacts[i].Areas);
            BodyCell(table.Cell(), even).Text(impacts[i].Severity);
            BodyCell(table.Cell(), even).Text(impacts[i].Note);
        }
    });

    private static void WorkList(IContainer container, IReadOnlyList<string> items) => container.Table(table =>
    {
        table.ColumnsDefinition(columns => columns.RelativeColumn());
        for (var i = 0; i < items.Count; i++)
        {
            BodyCell(table.Cell(), even: i % 2 == 1).Text(items[i]);
        }
    });

    private static void ValueBox(IContainer container, string label, string value) => container
        .PaddingTop(2, Unit.Millimetre)
        .ShowEntire()
        .Border(0.4f)
        .BorderColor(Rule)
        .Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(62);
                columns.RelativeColumn(38);
            });
            table.Cell()
                .Border(0.4f)
                .BorderColor(Rule)
                .Background(Shade)
                .Padding(3, Unit.Millimetre)
                .AlignMiddle()
                .Text(label)
                .FontSize(9)
                .Bold();
            table.Cell()
                .Border(0.4f)
                .BorderColor(Rule)
                .Padding(3, Unit.Millimetre)
                .AlignMiddle()
                .AlignCenter()
                .Text(value)
                .FontSize(12)
                .Bold()
                .FontColor(Brand);
        });

    /// <summary>
    /// The report's photo grid: two square frames per row, each printing the
    /// prepared square. Order is the snapshot's: Close-up first, Overview
    /// second, Supporting by its persisted order.
    /// </summary>
    private static void PhotoGrid(IContainer container, IReadOnlyList<PreparedReportPhoto> photos) => container.Column(grid =>
    {
        grid.Spacing(4, Unit.Millimetre);
        var pair = new List<PreparedReportPhoto>(2);
        void Flush()
        {
            if (pair.Count == 0)
            {
                return;
            }
            var first = pair[0];
            var second = pair.Count > 1 ? pair[1] : null;
            grid.Item().ShowEntire().Row(row =>
            {
                row.Spacing(4, Unit.Millimetre);
                PhotoFrame(row.RelativeItem(), first.Content);
                var right = row.RelativeItem();
                if (second is not null)
                {
                    PhotoFrame(right, second.Content);
                }
            });
            pair.Clear();
        }
        foreach (var photo in photos)
        {
            if (photo.FullPage)
            {
                Flush();
                grid.Item().PageBreak();
                grid.Item().ShowEntire().Column(page => PhotoFrame(page.Item(), photo.Content));
                continue;
            }
            pair.Add(photo);
            if (pair.Count == 2)
            {
                Flush();
            }
        }
        Flush();
    });

    /// <summary>
    /// The standalone image-pack paginator: each group starts on a fresh page
    /// after the first, ordinary photos are paired, and Full page photos are
    /// never grouped with another image.
    /// </summary>
    private static void ImagePackPhotos(IContainer container, IReadOnlyList<PreparedReportPhoto> photos) => container.Column(pack =>
    {
        pack.Spacing(4, Unit.Millimetre);
        var ordinary = new List<PreparedReportPhoto>(2);
        var hasGroup = false;

        void StartGroup()
        {
            if (hasGroup)
            {
                pack.Item().PageBreak();
            }

            hasGroup = true;
        }

        void FlushOrdinary()
        {
            if (ordinary.Count == 0)
            {
                return;
            }

            StartGroup();
            var first = ordinary[0];
            var second = ordinary.Count > 1 ? ordinary[1] : null;
            pack.Item().ShowEntire().Row(row =>
            {
                row.Spacing(4, Unit.Millimetre);
                PhotoFrame(row.RelativeItem(), first.Content);
                var right = row.RelativeItem();
                if (second is not null)
                {
                    PhotoFrame(right, second.Content);
                }
            });
            ordinary.Clear();
        }

        foreach (var photo in photos)
        {
            if (photo.FullPage)
            {
                FlushOrdinary();
                StartGroup();
                pack.Item().ShowEntire().Column(page => PhotoFrame(page.Item(), photo.Content));
                continue;
            }

            ordinary.Add(photo);
            if (ordinary.Count == 2)
            {
                FlushOrdinary();
            }
        }

        FlushOrdinary();
    });

    private static void PhotoFrame(IContainer container, byte[] square) => container
        .AspectRatio(1)
        .Border(0.4f)
        .BorderColor(Rule)
        .Image(square)
        .UseOriginalImage()
        .FitArea();

    /// <summary>
    /// The top-down damage diagram: one disc per recorded damage, drawn from
    /// its plan areas by the shared <see cref="DamageAreaGeometry"/> so the
    /// report shows what the Case workspace shows. Nothing is drawn when no
    /// damage names a plan area.
    /// </summary>
    private static void ImpactDiagram(ColumnDescriptor column, IReadOnlyList<ReportImpact> impacts)
    {
        var discs = impacts
            .Select(impact => DamageAreaGeometry.Disc(impact.Codes, PlanWidth, PlanHeight))
            .Where(disc => disc is not null)
            .Select(disc => disc!)
            .ToArray();
        if (discs.Length == 0)
        {
            return;
        }
        column.Item()
            .PaddingVertical(2, Unit.Millimetre)
            .ShowEntire()
            .Column(figure =>
            {
                DiagramLabel(figure.Item(), "FRONT");
                figure.Item().Height(75, Unit.Millimetre).AlignCenter().Svg(DiagramSvg(discs)).FitHeight();
                DiagramLabel(figure.Item(), "REAR");
            });
    }

    private static void DiagramLabel(IContainer container, string text) => container
        .AlignCenter()
        .Text(text)
        .FontSize(7)
        .SemiBold()
        .FontColor(DiagramInk)
        .LetterSpacing(0.08f);

    // The report's plan silhouette, and the body box the unit plan maps onto.
    private const string PlanViewBox = "20 0 200 390";
    private const string PlanBodyPath = "M75 62 L58 84 L58 316 L75 338 Q120 372 165 338 L182 316 L182 84 L165 62 Q120 8 75 62 Z";
    private const string PlanFrontGlassPath = "M92 100 Q120 86 148 100 L152 150 L88 150 Z";
    private const string PlanRearGlassPath = "M92 250 L148 250 L146 300 Q120 312 94 300 Z";
    private const string PlanStrongLinesPath = "M92 150 V250 M148 150 V250";
    private const string PlanLinesPath = "M58 150 H182 M58 250 H182";
    private const double PlanLeft = 58;
    private const double PlanTop = 8;
    private const double PlanWidth = 124;
    private const double PlanHeight = 364;
    private static readonly (int CentreX, int CentreY)[] PlanWheels = [(44, 96), (176, 96), (44, 286), (176, 286)];

    private static string DiagramSvg(IReadOnlyList<DamageDisc> discs)
    {
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{PlanViewBox}\">");
        foreach (var wheel in PlanWheels)
        {
            svg.Append(CultureInfo.InvariantCulture, $"<rect fill=\"#30383d\" x=\"{wheel.CentreX - 10}\" y=\"{wheel.CentreY - 22}\" width=\"20\" height=\"44\" rx=\"6\"/>");
        }
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"#ffffff\" stroke=\"#9ba7ad\" stroke-width=\"1.5\" d=\"{PlanBodyPath}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"#e9eef0\" stroke=\"#9ba7ad\" stroke-width=\"1\" d=\"{PlanFrontGlassPath}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"#e9eef0\" stroke=\"#9ba7ad\" stroke-width=\"1\" d=\"{PlanRearGlassPath}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"none\" stroke=\"#9ba7ad\" stroke-width=\"1\" d=\"{PlanStrongLinesPath}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"none\" stroke=\"#75828a\" stroke-width=\"1\" d=\"{PlanLinesPath}\"/>");
        foreach (var disc in discs)
        {
            svg.Append(CultureInfo.InvariantCulture, $"<circle fill=\"#f8dce1\" fill-opacity=\"0.75\" stroke=\"#c80a32\" stroke-width=\"1.5\" cx=\"{PlanLeft + disc.CentreX:0.#}\" cy=\"{PlanTop + disc.CentreY:0.#}\" r=\"{disc.Radius:0.#}\"/>");
        }
        foreach (var disc in discs)
        {
            svg.Append(CultureInfo.InvariantCulture, $"<circle fill=\"#c80a32\" cx=\"{PlanLeft + disc.CentreX:0.#}\" cy=\"{PlanTop + disc.CentreY:0.#}\" r=\"4\"/>");
        }
        svg.Append("</svg>");
        return svg.ToString();
    }

    // ---- Row and sentence composition -------------------------------------

    private static (string Label, string Value)[] VehicleRows(AssessmentReportSnapshot snapshot) =>
    [
        ("Make", snapshot.Vehicle.Make), ("Registration", snapshot.Vehicle.Registration),
        ("Model", snapshot.Vehicle.Model), ("VIN", snapshot.Vehicle.Vin ?? "—"),
        ("Odometer", snapshot.Vehicle.MileageDescription),
        ("Engine / Fuel", Join(" · ", snapshot.Vehicle.Engine, snapshot.Vehicle.Fuel)),
        ("VIN Checked", Flag(snapshot.Vehicle.VinChecked)),
        ("Transmission", snapshot.Vehicle.Transmission ?? "—"),
        ("Colour / Body", Join(" · ", snapshot.Vehicle.Colour, snapshot.Vehicle.Body)),
        ("Tax Expiry", Date(snapshot.Vehicle.TaxExpiry)), ("MOT Expiry", Date(snapshot.Vehicle.MotExpiry)),
        ("Airbags Deployed", snapshot.Vehicle.AirbagsDeployed ?? "—"),
        ("Fault Codes", snapshot.Vehicle.FaultCodes ?? "—"),
        ("Temporary Repairs Possible", Flag(snapshot.Vehicle.TemporaryRepairsPossible)),
        ("Temporary Repair Method", snapshot.Vehicle.TemporaryRepairMethod ?? "—"),
        ("Temporary Repair Cost", OptionalMoney(snapshot.Vehicle.TemporaryRepairCost)),
        ("Pre-Incident Condition", Display(snapshot.Vehicle.Condition)),
        ("Impact Magnitude", $"{Display(snapshot.ImpactSeverity)} — {Display(snapshot.ImpactLocation)}"),
    ];

    private static (string Label, string Value)[] VehicleDataRows(AssessmentReportSnapshot snapshot) =>
    [
        ("Retail Value", Money(snapshot.RetailValue)), ("Trade Value", Money(snapshot.TradeValue)),
        ("Engineer's Value", Money(snapshot.EngineerValue)), ("VIN", snapshot.Vehicle.Vin ?? "—"),
        ("Year", snapshot.Vehicle.Year), ("Odometer", snapshot.Vehicle.MileageDescription),
        ("Engine", snapshot.Vehicle.Engine ?? "—"), ("Fuel", snapshot.Vehicle.Fuel ?? "—"),
        ("Condition", Display(snapshot.Vehicle.Condition)),
    ];

    /// <summary>
    /// Unrelated damage is an output choice: with "Include unrelated damage"
    /// off the deduction row is omitted, not blanked. What was noted is the
    /// Unrelated Damage wording block's to say (v28 P30), so the table carries
    /// the money alone. The evidence itself is untouched.
    /// </summary>
    private static (string Label, string Value)[] DamageRows(AssessmentReportSnapshot snapshot)
    {
        var damage = snapshot.Damage;
        var rows = new List<(string, string)>();
        if (snapshot.Content.IncludeUnrelatedDamage)
        {
            rows.Add(("Unrelated Damage Deduction", OptionalMoney(damage.UnrelatedDeduction)));
        }
        rows.Add(("Paint / Material Transfer", damage.MaterialTransfer ?? "—"));
        return [.. rows];
    }

    private static (string Label, string Value)[] RestraintRows(ReportDamage damage) =>
    [
        ("Right Front Tyre / Belt", Join(" / ", damage.RightFrontTyre, damage.RightFrontBelt)),
        ("Left Front Tyre / Belt", Join(" / ", damage.LeftFrontTyre, damage.LeftFrontBelt)),
        ("Right Rear Tyre / Belt", Join(" / ", damage.RightRearTyre, damage.RightRearBelt)),
        ("Left Rear Tyre / Belt", Join(" / ", damage.LeftRearTyre, damage.LeftRearBelt)),
        ("Spare Tyre", damage.SpareTyre ?? "—"), ("Centre Belt", damage.CentreBelt ?? "—"),
    ];

    private static (string Label, string Value)[] SettlementRows(ReportSettlement settlement) =>
    [
        ("Excess", OptionalMoney(settlement.Excess)), ("Betterment", OptionalMoney(settlement.Betterment)),
        ("Claimant VAT Registered", Flag(settlement.ClaimantVatRegistered)), ("Reserve", OptionalMoney(settlement.Reserve)),
        ("Equity", Money(settlement.Equity)), ("Agreed Contract Sum", OptionalMoney(settlement.ContractSum)),
        ("Repair Delays", settlement.RepairDelays ?? "—"), ("Report Delay", settlement.ReportDelay ?? "—"),
        ("Storage Per Day", OptionalMoney(settlement.StoragePerDay)), ("Recovery", OptionalMoney(settlement.Recovery)),
        ("Hire Start", Date(settlement.HireStart)), ("Hire Daily Cost", OptionalMoney(settlement.HireDailyCost)),
        ("Diminution", OptionalMoney(settlement.Diminution)), ("Salvage At", settlement.SalvageAt ?? "—"),
        ("Salvage Agent", settlement.SalvageAgent ?? "—"), ("Salvage Agent Reference", settlement.SalvageAgentReference ?? "—"),
        ("Salvage Moved", Flag(settlement.SalvageMoved)), ("Owner Retains Salvage", Flag(settlement.SalvageOwnerRetains)),
        ("Salvage Value Agreed", Flag(settlement.SalvageValueAgreed)), ("Salvage Settled", Date(settlement.SalvageSettled)),
    ];

    private static (string Label, string Value, bool Highlight)[] Tiles(
        AssessmentReportSnapshot snapshot, AssessmentReportPresentation presentation) =>
        snapshot.Outcome == AssessmentReportOutcome.TotalLoss
            ?
            [
                ("Pre-Accident Value", Money(snapshot.EngineerValue), false),
                ("Repair Cost inc VAT", Money(snapshot.Costs.Total), false),
                ("Salvage Value", Money(snapshot.SalvageValue!.Value), false),
                ("Recommended Settlement", Money(presentation.RecommendedSettlement!.Value), true),
            ]
            :
            [
                ("Pre-Accident Value", Money(snapshot.EngineerValue), false),
                ("Labour Hours", Hours(snapshot.Costs.LabourHours), false),
                (snapshot.Outcome == AssessmentReportOutcome.CashInLieu ? "Cash in Lieu Settlement" : "Repair Cost inc VAT", Money(snapshot.Costs.Total), true),
            ];

    private static string Introduction(AssessmentReportSnapshot snapshot)
    {
        var location = snapshot.AssessmentMethod == "image_based"
            ? "Image Based Assessment"
            : snapshot.LocationAddress!;
        return $"In accordance with your instructions received on {Date(snapshot.InstructionsReceived)} requesting us to provide an independent accident damage report, we assessed the damage on {Date(snapshot.Assessed)}. Vehicle located at: {location}. Our findings are as detailed below.";
    }

    /// <summary>
    /// The accepted statement of truth, source-aware. The Glass's sentence is
    /// printed only when the operator turned "Disclose guide source" on and a
    /// Glass's valuation guide was actually used; otherwise it is omitted. No
    /// substitute sentence is written — the approved v3 specification supplies
    /// none (H5), and it names no other guide.
    /// </summary>
    private static IEnumerable<string> StatementOfTruth(AssessmentReportSnapshot snapshot)
    {
        yield return AssessmentReportContract.StatementOfTruth1;
        yield return AssessmentReportContract.StatementOfTruth2;
        if (snapshot.PrintsGuideDisclosure)
        {
            yield return AssessmentReportContract.StatementOfTruthGuide;
        }
        yield return AssessmentReportContract.StatementOfTruth3;
        yield return AssessmentReportContract.StatementOfTruth4;
    }

    private static string Display(string value) =>
        CultureInfo.GetCultureInfo("en-GB").TextInfo.ToTitleCase(value.Replace('_', ' ').ToLowerInvariant());
    private static string Flag(bool? value) => value switch { true => "Yes", false => "No", null => "—" };
    private static string OptionalMoney(decimal? value) => value is { } amount ? Money(amount) : "—";
    private static string Join(string separator, params string?[] values) =>
        string.Join(separator, values.Where(x => !string.IsNullOrWhiteSpace(x)));
}
