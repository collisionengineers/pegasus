/* Collision Engineers - Repair Cost Defence Report generator.
 *
 * Deterministic builder for a court-addressed, CPR-compliant report that
 * justifies Collision Engineers' repair costs and rebuts a defendant
 * insurer's challenge. Produces the fixed Collision Engineers house style
 * on EVERY invocation: logo header, Our/Your Ref block, "FAO The Court"
 * address, underlined centred title, RE line, red-ruled section headings,
 * summary table, point-by-point rebuttal, conclusion, statement of truth,
 * signature block and standard footer.
 *
 * Do NOT change the styling constants. The caller only supplies content via
 * the `data` object (see references/build_template.md for the schema).
 *
 * Usage:
 *   node build_report.js <data.json> <output.docx> [logoPath]
 */

const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  ImageRun, AlignmentType, BorderStyle, WidthType, ShadingType,
  VerticalAlign, LevelFormat, Header, Footer, TabStopType, TabStopPosition,
} = require("docx");

// ---- Fixed brand constants (do not vary between reports) -------------------
const BRAND_RED = "C8102E";       // Collision Engineers red
const BODY_GREY = "2B2B2B";       // near-black body text
const RULE_GREY = "BFBFBF";       // light grey table borders / footer rule
const HEADER_FILL = "C8102E";     // red table header fill
const ROW_ALT = "F2F2F2";         // alternating row shade

const FONT = "Arial";
const PAGE = { width: 11906, height: 16838 }; // A4 (matches example reports)
const MARGIN = 1134;                          // ~0.79in
const CONTENT_WIDTH = PAGE.width - MARGIN * 2; // 9638 DXA

const FOOTER_ADDRESS =
  "Collision Engineers, 77-79 Hoylake Road, Moreton, Wirral, CH46 9PY  |  engineers@collisionengineers.co.uk";
const WEBSITE = "www.CollisionEngineers.co.uk";

// ---- Helpers ---------------------------------------------------------------
function run(text, opts = {}) {
  return new TextRun({
    text: String(text == null ? "" : text),
    font: FONT,
    size: opts.size || 21,            // 10.5pt body default
    bold: !!opts.bold,
    italics: !!opts.italics,
    color: opts.color || BODY_GREY,
  });
}

function body(text, opts = {}) {
  return new Paragraph({
    spacing: { after: opts.after == null ? 160 : opts.after, line: 276 },
    alignment: opts.align || AlignmentType.JUSTIFIED,
    children: Array.isArray(text) ? text : [run(text, opts)],
  });
}

// Red-ruled section heading (bold caps with a brand-red bottom border).
function sectionHeading(text) {
  return new Paragraph({
    spacing: { before: 260, after: 120 },
    border: { bottom: { style: BorderStyle.SINGLE, size: 12, color: BRAND_RED, space: 2 } },
    children: [run(text.toUpperCase(), { bold: true, size: 22, color: BODY_GREY })],
  });
}

function bullet(text) {
  return new Paragraph({
    numbering: { reference: "ce-bullets", level: 0 },
    spacing: { after: 80, line: 264 },
    alignment: AlignmentType.JUSTIFIED,
    children: Array.isArray(text) ? text : [run(text)],
  });
}

function numbered(text, ref) {
  return new Paragraph({
    numbering: { reference: ref, level: 0 },
    spacing: { after: 100, line: 264 },
    alignment: AlignmentType.JUSTIFIED,
    children: Array.isArray(text) ? text : [run(text)],
  });
}

const cellBorder = { style: BorderStyle.SINGLE, size: 4, color: RULE_GREY };
const cellBorders = { top: cellBorder, bottom: cellBorder, left: cellBorder, right: cellBorder };

function dataCell(content, opts = {}) {
  const para = new Paragraph({
    alignment: opts.align || AlignmentType.LEFT,
    children: Array.isArray(content) ? content : [run(content, { bold: opts.bold, color: opts.color })],
  });
  return new TableCell({
    borders: cellBorders,
    width: { size: opts.width, type: WidthType.DXA },
    shading: opts.fill ? { fill: opts.fill, type: ShadingType.CLEAR, color: "auto" } : undefined,
    verticalAlign: VerticalAlign.CENTER,
    margins: { top: 60, bottom: 60, left: 110, right: 110 },
    children: [para],
  });
}

// Two-column "label / value" summary table.
function summaryTable(rows) {
  const W1 = 3400, W2 = CONTENT_WIDTH - W1;
  return new Table({
    width: { size: CONTENT_WIDTH, type: WidthType.DXA },
    columnWidths: [W1, W2],
    rows: rows.map(([label, value]) =>
      new TableRow({
        children: [
          dataCell(label, { width: W1, bold: true, fill: ROW_ALT }),
          dataCell(value, { width: W2 }),
        ],
      })
    ),
  });
}

// Headed comparison / cost table. columns = [{header, width, align}], data rows = arrays of strings.
function headedTable(columns, dataRows) {
  const widths = columns.map((c) => c.width);
  const headerRow = new TableRow({
    tableHeader: true,
    children: columns.map((c) =>
      dataCell([run(c.header, { bold: true, color: "FFFFFF" })], {
        width: c.width, fill: HEADER_FILL, align: c.align || AlignmentType.LEFT,
      })
    ),
  });
  const rows = [headerRow];
  dataRows.forEach((r, i) => {
    rows.push(new TableRow({
      children: r.map((val, j) =>
        dataCell(val, {
          width: widths[j],
          fill: i % 2 === 1 ? ROW_ALT : undefined,
          align: columns[j].align || AlignmentType.LEFT,
        })
      ),
    }));
  });
  return new Table({ width: { size: CONTENT_WIDTH, type: WidthType.DXA }, columnWidths: widths, rows });
}

// ---- Header / footer -------------------------------------------------------
function buildHeader(data, logoBuf) {
  const refLines = [];
  if (data.our_ref) refLines.push(["Our Ref: ", data.our_ref]);
  if (data.your_ref) refLines.push(["Your Ref: ", data.your_ref]);
  if (data.date) refLines.push(["Date: ", data.date]);

  const refParas = refLines.map(([k, v]) =>
    new Paragraph({
      alignment: AlignmentType.RIGHT,
      spacing: { after: 40 },
      children: [run(k, { bold: true }), run(v)],
    })
  );

  let logoCellChildren = [new Paragraph({ children: [run("COLLISION ENGINEERS", { bold: true, color: BRAND_RED })] })];
  if (logoBuf) {
    logoCellChildren = [new Paragraph({
      children: [new ImageRun({ type: "jpg", data: logoBuf, transformation: { width: 196, height: 113 } })],
    })];
  }

  const noBorder = { style: BorderStyle.NONE, size: 0, color: "FFFFFF" };
  const none = { top: noBorder, bottom: noBorder, left: noBorder, right: noBorder };

  const headerTable = new Table({
    width: { size: CONTENT_WIDTH, type: WidthType.DXA },
    columnWidths: [4800, CONTENT_WIDTH - 4800],
    borders: { top: noBorder, bottom: noBorder, left: noBorder, right: noBorder, insideHorizontal: noBorder, insideVertical: noBorder },
    rows: [new TableRow({
      children: [
        new TableCell({ borders: none, width: { size: 4800, type: WidthType.DXA }, children: logoCellChildren }),
        new TableCell({ borders: none, width: { size: CONTENT_WIDTH - 4800, type: WidthType.DXA }, verticalAlign: VerticalAlign.TOP, children: refParas.length ? refParas : [new Paragraph({ children: [run("")] })] }),
      ],
    })],
  });

  return new Header({ children: [headerTable, new Paragraph({ spacing: { after: 60 }, children: [run("")] })] });
}

function buildFooter() {
  return new Footer({
    children: [
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 60, after: 0 },
        border: { top: { style: BorderStyle.SINGLE, size: 4, color: RULE_GREY, space: 4 } },
        children: [run(WEBSITE, { size: 16, color: BRAND_RED })],
      }),
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 20 },
        children: [run(FOOTER_ADDRESS, { size: 14, color: "808080" })],
      }),
    ],
  });
}

// ---- Document assembly -----------------------------------------------------
function buildReport(data, outPath, logoPath) {
  let logoBuf = null;
  if (logoPath && fs.existsSync(logoPath)) logoBuf = fs.readFileSync(logoPath);

  const children = [];

  // Addressee block (FAO The Court + care-of address lines).
  children.push(new Paragraph({ spacing: { after: 20 }, children: [run(data.addressee_title || "FAO The Court", { bold: true })] }));
  (data.addressee_lines || []).forEach((l) =>
    children.push(new Paragraph({ spacing: { after: 20 }, children: [run(l)] }))
  );

  // Title (underlined, bold, centred) + RE line.
  children.push(new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { before: 320, after: 80 },
    children: [run((data.title || "REPAIR COST DEFENCE REPORT").toUpperCase(), { bold: true, size: 24, underline: true })],
  }));
  if (data.re_line) {
    children.push(new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { after: 220 },
      children: [run("RE: " + data.re_line, { bold: true, size: 22 })],
    }));
  }

  // Salutation + intro.
  children.push(body(data.salutation || "Dear Sirs,", { after: 160, align: AlignmentType.LEFT }));
  (data.intro_paragraphs || []).forEach((p) => children.push(body(p)));

  // Summary of the dispute table.
  if (data.summary_rows && data.summary_rows.length) {
    children.push(sectionHeading(data.summary_heading || "Summary of the Matter in Dispute"));
    children.push(summaryTable(data.summary_rows));
    if (data.summary_note) children.push(body(data.summary_note, { after: 120 }));
    else children.push(new Paragraph({ spacing: { after: 80 }, children: [run("")] }));
  }

  // Free-form ordered sections.
  (data.sections || []).forEach((sec) => {
    children.push(sectionHeading(sec.heading));
    (sec.paragraphs || []).forEach((p) => children.push(body(p)));
    if (sec.bullets) sec.bullets.forEach((b) => children.push(bullet(b)));
    if (sec.table) {
      children.push(headedTable(sec.table.columns, sec.table.rows));
      if (sec.table.note) children.push(body(sec.table.note, { after: 80 }));
      else children.push(new Paragraph({ spacing: { after: 60 }, children: [run("")] }));
    }
    // Numbered rebuttal points: each point can have a "challenge" + "response".
    if (sec.rebuttals) {
      const ref = sec.__rebRef || "reb-pool-0";
      sec.rebuttals.forEach((rb) => {
        if (typeof rb === "string") {
          children.push(numbered([run(rb)], ref));
        } else {
          children.push(numbered([run(rb.challenge, { bold: true })], ref));
          if (rb.response) children.push(body(rb.response, { after: 120 }));
        }
      });
    }
    (sec.after_paragraphs || []).forEach((p) => children.push(body(p)));
  });

  // Conclusion.
  if (data.conclusion_paragraphs && data.conclusion_paragraphs.length) {
    children.push(sectionHeading("Conclusion"));
    data.conclusion_paragraphs.forEach((p) => children.push(body(p)));
  }

  // CPR 35.6 availability line (standard).
  children.push(body(
    data.cpr_line ||
    "We trust the foregoing assists the Court. We remain available to provide any further clarification that may be required, subject to Civil Procedure Rule 35.6.",
    { after: 160 }
  ));

  // Statement of truth (fixed wording unless overridden).
  children.push(sectionHeading("Statement of Truth"));
  children.push(body(
    data.statement_of_truth ||
    "I declare that I understand my duty in providing this report to the Court and I confirm that I have complied with that duty. I understand that this duty overrides any other obligation. The opinions expressed in this report represent my true and complete professional opinion on the matters to which they refer. The facts stated in this report are true to the best of my knowledge and belief.",
    { after: 280 }
  ));

  // Signature block.
  const sig = data.signatory || {};
  children.push(new Paragraph({ spacing: { after: 20 }, children: [run(sig.name || "A. Patterson", { bold: true })] }));
  (sig.lines || ["M.Inst.IAEA", "Independent Motor Engineer", "Collision Engineers Ltd", "engineers@collisionengineers.co.uk"]).forEach((l, i) =>
    children.push(new Paragraph({ spacing: { after: 20 }, children: [run(l, { italics: /Independent Motor Engineer/.test(l) })] }))
  );
  children.push(new Paragraph({ spacing: { after: 20 }, children: [run("Date: " + (data.date || ""))] }));

  const doc = new Document({
    creator: "Collision Engineers Ltd",
    title: data.title || "Repair Cost Defence Report",
    styles: { default: { document: { run: { font: FONT, size: 21, color: BODY_GREY } } } },
    numbering: {
      config: [
        { reference: "ce-bullets", levels: [{ level: 0, format: LevelFormat.BULLET, text: "\u2022", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 280 } } } }] },
      ].concat(
        // Pre-register a pool of independent decimal numberings for rebuttal sections.
        Array.from({ length: 12 }, (_, i) => ({
          reference: "reb-pool-" + i,
          levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 360 } } } }],
        }))
      ),
    },
    sections: [{
      properties: { page: { size: { width: PAGE.width, height: PAGE.height }, margin: { top: 1700, right: MARGIN, bottom: 1200, left: MARGIN } } },
      headers: { default: buildHeader(data, logoBuf) },
      footers: { default: buildFooter() },
      children,
    }],
  });

  return Packer.toBuffer(doc).then((buf) => { fs.writeFileSync(outPath, buf); return outPath; });
}

// ---- CLI -------------------------------------------------------------------
if (require.main === module) {
  const [dataPath, outPath, logoArg] = process.argv.slice(2);
  if (!dataPath || !outPath) {
    console.error("Usage: node build_report.js <data.json> <output.docx> [logoPath]");
    process.exit(1);
  }
  const data = JSON.parse(fs.readFileSync(dataPath, "utf8"));
  const logoPath = logoArg || path.join(__dirname, "..", "assets", "logo.jpeg");

  // Assign pool numbering refs to rebuttal sections deterministically.
  let poolIdx = 0;
  (data.sections || []).forEach((sec) => {
    if (sec.rebuttals) { sec.__rebRef = "reb-pool-" + (poolIdx++ % 12); }
  });

  buildReport(data, outPath, logoPath)
    .then((p) => console.log("Wrote " + p))
    .catch((e) => { console.error(e); process.exit(1); });
}

module.exports = { buildReport };
