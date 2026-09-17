using System.Globalization;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Reports;
using QuestPDF.Elements.Table;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The printed images one render carries: every report photo as the square
/// the page prints (EXIF orientation, the Engineer's rotation and crop
/// already applied, in snapshot order) plus the signature, all decoded
/// once before composition so an undecodable image fails the render closed
/// instead of printing a placeholder.
/// </summary>
internal sealed record PreparedReportImages(
    IReadOnlyList<byte[]> Photos,
    byte[] Signature,
    byte[] Logo);

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
    /// <summary>The report family every printed run uses; embedded, never a system font.</summary>
    internal const string FontFamily = "Liberation Sans";

    /// <summary>The DATA register: body copy, tables and the fee note.</summary>
    private const float DataRegister = 8.8f;

    /// <summary>The LETTER register: the closing and the signatory's printed name.</summary>
    private const float LetterRegister = 10f;

    private const float BodyLineHeight = 1.22f;
    private const float SectionGap = 3.5f;
    private const float ParagraphGap = 3.1f;
    private const float HeadingGap = 2f;

    private static readonly Color Ink = Color.FromHex("#222222");
    private static readonly Color Muted = Color.FromHex("#555555");
    private static readonly Color Brand = Color.FromHex("#c80a32");
    private static readonly Color Charcoal = Color.FromHex("#2c2a27");
    private static readonly Color Rule = Color.FromHex("#bebebe");
    private static readonly Color Zebra = Color.FromHex("#f5f5f5");
    private static readonly Color Shade = Color.FromHex("#f2f2f2");
    private static readonly Color DiagramInk = Color.FromHex("#75828a");

    private const string CompanyName = "Collision Engineers Ltd";
    private const string CompanyEmail = "Engineers@CollisionEngineers.co.uk";
    private const string CompanyWebsite = "www.CollisionEngineers.co.uk";
    private const string FooterSeparator = " | ";

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
            CaseReportArtifactKind.AssessmentReport => false,
            _ => throw new ReportRenderRejectedException($"Unsupported report artifact kind '{kind}'."),
        };
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
            if (!feeNote && snapshot.IncludeFeeNote)
            {
                AddPages(pageIsFeeNote: true);
            }
        });
    }

    private static void Footer(IContainer container, AssessmentReportSnapshot snapshot, bool feeNote)
    {
        var centre = feeNote
            ? $"{snapshot.Vehicle.Registration} · {snapshot.OurReference}{FooterSeparator}{CompanyName}{FooterSeparator}VAT No: {AssessmentReportContract.VatNumber}"
            : $"{snapshot.Vehicle.Registration} · {snapshot.OurReference}{FooterSeparator}{CompanyName}{FooterSeparator}{CompanyWebsite}";
        container.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted)).Row(row =>
        {
            row.RelativeItem().Text(centre);
            row.AutoItem().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
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

        Section(column, "Nature of Incident", section => Paragraph(
            section,
            $"The vehicle has suffered {Display(snapshot.ImpactSeverity)} collision/impact damage to the {Display(snapshot.ImpactLocation)}."));

        Section(column, "Damage", section =>
        {
            ImpactDiagram(section, snapshot.Damage.Impacts);
            ImpactTable(section.Item(), snapshot.Damage.Impacts);
            DataTable(section.Item(), 46, DamageRows(snapshot));
        });

        Section(column, "Tyres and Seat Belts", section =>
            DataTable(section.Item(), 40, RestraintRows(snapshot.Damage)));

        Section(column, "Engineer's Comments", section =>
        {
            Paragraph(section, MileageSentence(snapshot.Vehicle.MileageSource));
            if (snapshot.LegalStatus.Equals("unroadworthy", StringComparison.OrdinalIgnoreCase))
            {
                Paragraph(section, $"Please note the vehicle is unroadworthy due to {snapshot.UnroadworthyReason}.");
            }
            if (!string.IsNullOrWhiteSpace(snapshot.EngineerComments))
            {
                Paragraph(section, snapshot.EngineerComments);
            }
        });

        Section(column, "Vehicle History Check", section => Paragraph(section, snapshot.HistoryCheck));
        Section(column, "Pre-Incident Condition", section => Paragraph(
            section,
            $"The vehicle is considered to be in {Display(snapshot.Vehicle.Condition)} condition for its age and type."));

        Section(column, presentation.SettlementHeading, section =>
        {
            Paragraph(section, presentation.SettlementText);
            ValueBox(section.Item(), presentation.SettlementLabel, Money(presentation.RecommendedSettlement!.Value));
            DataTable(section.Item(), 46, SettlementRows(snapshot.Settlement));
        });

        if (snapshot.Outcome == AssessmentReportOutcome.TotalLoss)
        {
            Section(column, "Salvage", section => Paragraph(
                section,
                "Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category S (structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk. "
                + $"We suggest that the sale of the salvage will realise in the order of {Money(snapshot.SalvageValue!.Value)}. We have not taken any action towards removal of the salvage at this time."));
        }

        column.Item().PageBreak();
        Section(column, "Vehicle Data", section =>
        {
            DataTable(section.Item(), 32, VehicleDataRows(snapshot));
            if (snapshot.Content.IncludeValuationCommentary && !string.IsNullOrWhiteSpace(snapshot.ValuationCommentary))
            {
                Paragraph(section, snapshot.ValuationCommentary);
            }
        });
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

    private static void Letterhead(ColumnDescriptor column, byte[] logo, Action<IContainer> rightHandBlock) =>
        column.Item().PaddingBottom(9, Unit.Millimetre).Row(row =>
        {
            row.RelativeItem()
                .PaddingLeft(7, Unit.Millimetre)
                .AlignLeft()
                .Width(53, Unit.Millimetre)
                .Height(30.3f, Unit.Millimetre)
                .Image(logo)
                .WithCompressionQuality(ImageCompressionQuality.VeryHigh)
                .FitArea();
            var right = row.AutoItem().PaddingTop(10, Unit.Millimetre).MinWidth(54, Unit.Millimetre);
            rightHandBlock(right);
        });

    private static void Reference(ColumnDescriptor column, string label, string value) => column.Item().Row(row =>
    {
        row.ConstantItem(18, Unit.Millimetre)
            .PaddingVertical(1, Unit.Millimetre)
            .PaddingHorizontal(2, Unit.Millimetre)
            .AlignRight()
            .Text(label)
            .Bold()
            .FontColor(Color.FromHex("#111111"));
        row.AutoItem()
            .PaddingVertical(1, Unit.Millimetre)
            .PaddingLeft(6, Unit.Millimetre)
            .Text(value)
            .Bold();
    });

    private static void Title(ColumnDescriptor column, string title, bool italic)
    {
        var text = column.Item()
            .PaddingBottom(1, Unit.Millimetre)
            .AlignCenter()
            .Text(title.ToUpperInvariant())
            .FontSize(14)
            .Bold()
            .FontColor(Brand);
        if (italic)
        {
            text.Italic().LetterSpacing(0.08f);
        }
        column.Item()
            .PaddingTop(1, Unit.Millimetre)
            .PaddingBottom(4, Unit.Millimetre)
            .LineHorizontal(1.5f)
            .LineColor(Brand);
    }

    private static void Badge(IContainer container, string text, Color background) => container
        .Background(background)
        .PaddingVertical(2, Unit.Millimetre)
        .PaddingHorizontal(3, Unit.Millimetre)
        .Text(text)
        .Bold()
        .FontColor(Colors.White);

    private static void Tile(IContainer container, string label, string value, bool highlight)
    {
        var ink = highlight ? Colors.White : Ink;
        container
            .Border(0.4f)
            .BorderColor(Rule)
            .Background(highlight ? Brand : Colors.White)
            .Padding(2, Unit.Millimetre)
            .Column(tile =>
            {
                tile.Item().AlignCenter().Text(label.ToUpperInvariant()).FontSize(7.5f).Bold().FontColor(ink);
                tile.Item().PaddingTop(1, Unit.Millimetre).AlignCenter().Text(value).FontSize(11).Bold().FontColor(ink);
            });
    }

    /// <summary>
    /// One titled section: the heading under its brand rule, then the body
    /// items. Sections are separated by the accepted section gap; a heading
    /// stays with the first body item.
    /// </summary>
    private static void Section(ColumnDescriptor column, string title, Action<ColumnDescriptor> body, float bottomGap = 0)
    {
        column.Item()
            .PaddingTop(SectionGap, Unit.Millimetre)
            .PaddingBottom(bottomGap, Unit.Millimetre)
            .Column(section =>
            {
                section.Item()
                    .PaddingBottom(HeadingGap, Unit.Millimetre)
                    .BorderBottom(1.5f)
                    .BorderColor(Brand)
                    .PaddingBottom(1, Unit.Millimetre)
                    .Text(title)
                    .FontSize(10.6f)
                    .Bold();
                body(section);
            });
    }

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

    /// <summary>A brand-filled column heading of the data register's tables.</summary>
    private static void HeaderCell(
        IContainer cell,
        string text,
        float fontSize = DataRegister,
        bool alignRight = false,
        float verticalPadding = 1.4f,
        float horizontalPadding = 1.4f)
    {
        var box = cell
            .Background(Brand)
            .PaddingVertical(verticalPadding, Unit.Millimetre)
            .PaddingHorizontal(horizontalPadding, Unit.Millimetre);
        if (alignRight)
        {
            box = box.AlignRight();
        }
        box.Text(text).FontSize(fontSize).Bold().FontColor(Colors.White);
    }

    /// <summary>
    /// The red-bordered label/value table: brand-filled label cells and
    /// bordered value cells at 9.2pt, without zebra striping.
    /// </summary>
    private static void DataTable(IContainer container, float labelWidthMillimetres, IEnumerable<(string Label, string Value)> rows) => container
        .PaddingTop(1, Unit.Millimetre)
        .PaddingBottom(3, Unit.Millimetre)
        .Border(1.2f)
        .BorderColor(Brand)
        .DefaultTextStyle(style => style.FontSize(9.2f))
        .Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(labelWidthMillimetres, Unit.Millimetre);
                columns.RelativeColumn();
            });
            foreach (var (label, value) in rows)
            {
                table.Cell()
                    .Background(Brand)
                    .Padding(1.4f, Unit.Millimetre)
                    .AlignMiddle()
                    .Text(label)
                    .Bold()
                    .FontColor(Colors.White);
                table.Cell()
                    .Border(0.6f)
                    .BorderColor(Brand)
                    .PaddingVertical(1.8f, Unit.Millimetre)
                    .PaddingHorizontal(2, Unit.Millimetre)
                    .AlignMiddle()
                    .Text(value);
            }
        });

    /// <summary>A bordered, zebra-striped body cell of the data register's plain table.</summary>
    private static IContainer BodyCell(ITableCellContainer cell, bool even) => cell
        .Border(0.4f)
        .BorderColor(Rule)
        .Background(even ? Zebra : Colors.White)
        .Padding(1.4f, Unit.Millimetre);

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
            HeaderCell(header.Cell(), "Zone");
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
            BodyCell(table.Cell(), even).Text(impacts[i].Zone);
            BodyCell(table.Cell(), even).Text(impacts[i].Severity);
            BodyCell(table.Cell(), even).Text(impacts[i].Note);
        }
    });

    private static void CostTable(IContainer container, ReportRepairCosts costs) => container.Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn();
            columns.ConstantColumn(35, Unit.Millimetre);
        });
        table.Header(header =>
        {
            HeaderCell(header.Cell(), "Item");
            HeaderCell(header.Cell(), "Amount", alignRight: true);
        });
        var rows = CostRows(costs);
        for (var i = 0; i < rows.Length; i++)
        {
            var even = i % 2 == 1;
            BodyCell(table.Cell(), even).Text(rows[i].Label);
            BodyCell(table.Cell(), even).AlignRight().Text(rows[i].Value);
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
    /// The photo grid: two square frames per row, each printing the prepared
    /// square, a row never split across pages. Order is the snapshot's:
    /// Close-up first, Overview second, Supporting by its persisted order.
    /// </summary>
    private static void PhotoGrid(IContainer container, IReadOnlyList<byte[]> photos) => container.Column(grid =>
    {
        grid.Spacing(4, Unit.Millimetre);
        for (var i = 0; i < photos.Count; i += 2)
        {
            var first = photos[i];
            var second = i + 1 < photos.Count ? photos[i + 1] : null;
            grid.Item().ShowEntire().Row(row =>
            {
                row.Spacing(4, Unit.Millimetre);
                PhotoFrame(row.RelativeItem(), first);
                var right = row.RelativeItem();
                if (second is not null)
                {
                    PhotoFrame(right, second);
                }
            });
        }
    });

    private static void PhotoFrame(IContainer container, byte[] square) => container
        .AspectRatio(1)
        .Border(0.4f)
        .BorderColor(Rule)
        .Image(square)
        .UseOriginalImage()
        .FitArea();

    /// <summary>
    /// The marked top-down damage diagram, drawn from the shared
    /// <see cref="DamageDiagramGeometry"/> so the report shows the same
    /// selected regions as the Case workspace. Nothing is drawn when no
    /// impact carries a canonical zone code.
    /// </summary>
    private static void ImpactDiagram(ColumnDescriptor column, IReadOnlyList<ReportImpact> impacts)
    {
        var marked = impacts
            .Where(impact => !string.IsNullOrWhiteSpace(impact.Code))
            .Select(impact => Slug(impact.Code))
            .ToHashSet(StringComparer.Ordinal);
        if (marked.Count == 0)
        {
            return;
        }
        column.Item()
            .PaddingVertical(2, Unit.Millimetre)
            .ShowEntire()
            .Column(figure =>
            {
                DiagramLabel(figure.Item(), "FRONT");
                figure.Item().Height(75, Unit.Millimetre).AlignCenter().Svg(DiagramSvg(marked)).FitHeight();
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

    private static string DiagramSvg(HashSet<string> marked)
    {
        const string markedStyle = "fill=\"#f8dce1\" stroke=\"#c80a32\" stroke-width=\"1.5\"";
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{DamageDiagramGeometry.ViewBox}\">");
        foreach (var wheel in DamageDiagramGeometry.Wheels)
        {
            svg.Append(CultureInfo.InvariantCulture, $"<rect fill=\"#30383d\" x=\"{wheel.CentreX - 10}\" y=\"{wheel.CentreY - 22}\" width=\"20\" height=\"44\" rx=\"6\"/>");
            if (marked.Contains(Slug(wheel.Code)))
            {
                svg.Append(CultureInfo.InvariantCulture, $"<rect {markedStyle} x=\"{wheel.CentreX - 14}\" y=\"{wheel.CentreY - 26}\" width=\"28\" height=\"52\" rx=\"8\"/>");
            }
        }
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"#ffffff\" stroke=\"#9ba7ad\" stroke-width=\"1.5\" d=\"{DamageDiagramGeometry.BodyPath}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"#e9eef0\" stroke=\"#9ba7ad\" stroke-width=\"1\" d=\"{DamageDiagramGeometry.FrontGlassPath}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"#e9eef0\" stroke=\"#9ba7ad\" stroke-width=\"1\" d=\"{DamageDiagramGeometry.RearGlassPath}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"none\" stroke=\"#9ba7ad\" stroke-width=\"1\" d=\"{DamageDiagramGeometry.StrongStructuralLinesPath}\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<path fill=\"none\" stroke=\"#75828a\" stroke-width=\"1\" d=\"{DamageDiagramGeometry.StructuralLinesPath}\"/>");
        foreach (var zone in DamageDiagramGeometry.Zones.Where(zone => marked.Contains(Slug(zone.Code))))
        {
            svg.Append(CultureInfo.InvariantCulture, $"<path {markedStyle} d=\"{zone.Path}\"/>");
        }
        foreach (var marker in DamageDiagramGeometry.Markers.Where(marker => marked.Contains(Slug(marker.Code))))
        {
            svg.Append(CultureInfo.InvariantCulture, $"<g transform=\"translate({marker.CentreX - 9} {marker.CentreY - 9})\"><circle cx=\"9\" cy=\"9\" r=\"9\" fill=\"#c80a32\"/><path transform=\"translate(3 3) scale(.5)\" fill=\"none\" stroke=\"#ffffff\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M14.5 0 3 14h8l-1.5 10L21 9h-8z\"/></g>");
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
    /// off the two unrelated rows are omitted, not blanked. The evidence
    /// itself is untouched.
    /// </summary>
    private static (string Label, string Value)[] DamageRows(AssessmentReportSnapshot snapshot)
    {
        var damage = snapshot.Damage;
        var rows = new List<(string, string)>();
        if (snapshot.Content.IncludeUnrelatedDamage)
        {
            rows.Add(("Unrelated Damage", damage.Unrelated ?? "—"));
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
        ("Equity", Money(settlement.Equity)), ("Repair Duration", settlement.RepairDays is { } days ? $"{days} days" : "—"),
        ("Repair Delays", settlement.RepairDelays ?? "—"), ("Report Delay", settlement.ReportDelay ?? "—"),
        ("Storage Per Day", OptionalMoney(settlement.StoragePerDay)), ("Recovery", OptionalMoney(settlement.Recovery)),
        ("Hire Start", Date(settlement.HireStart)), ("Hire Daily Cost", OptionalMoney(settlement.HireDailyCost)),
        ("Diminution", OptionalMoney(settlement.Diminution)), ("Salvage At", settlement.SalvageAt ?? "—"),
        ("Salvage Agent", settlement.SalvageAgent ?? "—"), ("Salvage Agent Reference", settlement.SalvageAgentReference ?? "—"),
        ("Salvage Moved", Flag(settlement.SalvageMoved)), ("Owner Retains Salvage", Flag(settlement.SalvageOwnerRetains)),
        ("Salvage Value Agreed", Flag(settlement.SalvageValueAgreed)), ("Salvage Settled", Date(settlement.SalvageSettled)),
    ];

    /// <summary>
    /// The Current estimate's canonical printed breakdown. Hours and the
    /// hourly rate are descriptive; the five printed components, the printed
    /// sub total, the printed VAT and the printed total are the estimate's
    /// own figures and reconcile exactly.
    /// </summary>
    private static (string Label, string Value)[] CostRows(ReportRepairCosts costs) =>
    [
        ("Labour Hours", Hours(costs.LabourHours)),
        ("Paint Hours", Hours(costs.PaintHours)),
        ("Hourly Rate", Money(costs.HourlyRate)),
        ("Parts", Money(costs.Printed.Parts)),
        ("Panel Labour", Money(costs.Printed.PanelLabour)),
        ("Paint Labour", Money(costs.Printed.PaintLabour)),
        ("Paint Materials", Money(costs.Printed.Materials)),
        ("Specialist / Other", Money(costs.Printed.Specialist)),
        ("Sub Total", Money(costs.Printed.Net)),
        (costs.VatLabel, Money(costs.Printed.Vat)),
        ("Total Estimated Repair Cost", Money(costs.Total)),
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

    private static string MileageSentence(string source) => source switch
    {
        "online_data" => "The mileage has been calculated from online data.",
        "owner" => "The mileage has been provided by the owner.",
        "repairer" => "The mileage has been provided by the repairer.",
        "principal" => "The mileage has been provided by the instructing principal.",
        "average" => "The mileage has been calculated from average mileage data.",
        "tbc" => "The mileage is to be confirmed.",
        _ => throw new ReportRenderRejectedException("Unsupported mileage source."),
    };

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
    private static string Hours(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Flag(bool? value) => value switch { true => "Yes", false => "No", null => "—" };
    private static string Date(DateOnly value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    private static string Date(DateOnly? value) => value is { } date ? Date(date) : "—";
    private static string OptionalMoney(decimal? value) => value is { } amount ? Money(amount) : "—";
    private static string Join(string separator, params string?[] values) =>
        string.Join(separator, values.Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string Money(decimal value) => value.ToString("£#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
    private static string Number(decimal value) => value.ToString("#,##0.00", CultureInfo.GetCultureInfo("en-GB"));
    internal static string Slug(string value) => new(value.ToUpperInvariant().Select(x => char.IsLetterOrDigit(x) ? x : '_').ToArray());
}
