"""
Audatex-style PDF generator — v4 (dynamic pagination, routing-aware).

IMPORTANT NOTE FOR FUTURE CLAUDE:
In EVA, the last row of the Parts screen is ALWAYS an empty row with Type
dropdown set to R&R — it's EVA's manual-entry row, allowing the engineer
to click and add a custom line. It is NOT a ghost row from our PDF parser.
Do not try to "fix" it.

During development, some rows appeared to contain text like "Zh/4" or "/4"
which I misread as fragmented version-string text. On closer inspection,
these were either the manual-entry row or highlighting artefacts in the
screenshots. The PDF parser is working correctly.

Changes from v3:
  - Dynamic pagination via PageWriter — tables flow across any number of pages
  - Removed STANDARD auto-inclusion (AI is now responsible for full operations list)
  - No continuation-page headers on tables (real Audatex doesn't use them)
  - Font sizes match real Audatex (Verdana 12pt chrome, 10pt headers, 9pt body)
  - Chrome y-positions match real Audatex (22, 37, 52, 66, 81, 95)
  - CONTENT_TOP=188, CONTENT_BOTTOM=750 for generous parser safe zones
  - Two-pass build for accurate "PAGE X OF Y"
  - Description truncation with ellipsis for long descriptions
  - Asterisk markers on unpriced parts

Rulebook (unchanged from v3):
  Operation type      → PDF section        → EVA Type     → Engineer's Report
  ──────────────────────────────────────────────────────────────────────────
  new_part            → Parts              → New          → Main new parts
  repair              → Labour (w/ REPAIR) → Repair       → Repairs
  rnr                 → Labour             → R&R          → (hidden)
  check_labour        → Labour (w/ CHECK)  → Check        → Additional ops
  paint_new           → Paint (NEW PART)   → Paint        → Additional ops
  paint_repair        → Paint (REPAIR)     → Paint        → Additional ops
  paint_blend         → Paint (SURFACE)    → Blend        → Additional ops
  paint_prep          → Paint (PREP)       → Paint        → Additional ops
  specialist_fixed    → Extras             → Specialist   → Additional ops
  specialist_wu       → Extras (£=WU×rate) → Specialist   → Additional ops

Coordinate positions (verified across 5 real Audatex PDFs):
  Labour:  Guide x=20.0    Desc x=158.75   WU right x=547.15
  Parts:   Guide x=20.0    Desc x=103.25   PartNum x=242.0   Bet x=353.0   Price right x=547.15
  Extras:  Desc x=103.25   Specialist x=214.25   Price right x=547.15
  Cost:    Labels x=348.0  Values right x=549.7
"""

import io
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.pdfmetrics import stringWidth

PAGE_W, PAGE_H = A4  # 595.276 x 841.89

# ─── Anchored coordinates ────────────────────────────────────────────────────
LAB_GUIDE_X      = 20.0
LAB_DESC_X       = 158.75
LAB_WU_RIGHT_X   = 547.15

PRT_GUIDE_X      = 20.0
PRT_DESC_X       = 103.25
PRT_PARTNUM_X    = 242.0
PRT_BET_X        = 353.0
PRT_PRICE_RIGHT  = 547.15

EXT_DESC_X       = 103.25
EXT_SPEC_X       = 214.25
EXT_BET_X        = 325.2
EXT_PRICE_RIGHT  = 547.15

COST_LABEL_X     = 348.0
COST_VAL_RIGHT   = 549.7

VEH_LABEL_X      = 20.0
VEH_VAL_X        = 158.75
VEH_SPECS_X      = 325.0

CLAIM_COL1_X     = 20.0
CLAIM_COL2_X     = 158.75
CLAIM_COL3_X     = 325.0
CLAIM_COL4_X     = 463.0

ROW_H_TABLE      = 12.14
ROW_H_SUMMARY    = 10.9

FONT            = 'Helvetica'
FONT_BOLD       = 'Helvetica-Bold'

# Font sizes — measured from real Audatex PDFs (Verdana-Bold 12pt chrome,
# Verdana-Bold 10pt section headers, Verdana 9pt body, Verdana-Bold 10pt WU values).
# We use Helvetica instead of Verdana; EVA doesn't care about the font face,
# only about the layout. Matching sizes keeps us within EVA's parser expectations.
SIZE_HEADER     = 12          # Company block (TEL: COLLISION ENGINEERS etc.)
SIZE_SUBHDR     = 12          # Assessment Number, Version, etc.
SIZE_H2         = 12          # Section headers (Cost Summary, Addresses, etc.)
SIZE_BODY       = 10          # LABOUR / PARTS / Extras section labels
SIZE_TABLE_HDR  = 10          # Column headers (Number, Description, Work Units)
SIZE_TABLE      = 9           # Table data rows
SIZE_WU_BOLD    = 10          # Work Unit values (bold right-aligned)
SIZE_FOOTER     = 9

# y-coordinates of usable content area.
# Real Audatex PDFs always start content at y≈188 on every page.
# EVA's parser treats y<~170 as the page-chrome safe zone — any text in that
# band can be misread as a ghost row. So we match the real Audatex convention
# and leave a ~90-point gap below the chrome for the parser's safety.
CONTENT_TOP     = 188.0
# Real Audatex stops content around y=750, leaving ~55pt of blank space above
# the footer. If we fill too close to the footer, EVA's parser gets confused
# about where the table ends and where the page chrome of the next page begins.
CONTENT_BOTTOM  = 750.0


# ─── PageWriter — handles pagination automatically ───────────────────────────
class PageWriter:
    """
    Wraps a reportlab canvas and tracks the current y-cursor.

    Callers use emit_row() / emit_rule() / emit_space() to add content;
    the writer breaks to a new page automatically when the cursor would
    spill past CONTENT_BOTTOM.

    On each new page, it redraws page chrome and invokes an optional
    'on_new_page' callback (used to redraw table headers).
    """

    def __init__(self, canvas_obj, assessment_number, version, printed):
        self.c = canvas_obj
        self.assessment_number = assessment_number
        self.version = version
        self.printed = printed
        self.y = CONTENT_TOP
        self.page_num = 1
        self.on_new_page = None   # callback for continuation headers
        self._draw_chrome()

    # ── Low-level drawing ───────────────────────────────────────────────────
    def _draw_chrome(self):
        c = self.c
        # Chrome y-positions match real Audatex exactly (measured from TEST_3
        # after accounting for ReportLab's baseline rendering of 12pt).
        #   Line 1 TEL: COLLISION ENGINEERS     renders at y ≈ 22.5
        #   Line 2 phone + address              renders at y ≈ 37.1
        #   Line 3 MORETON                      renders at y ≈ 51.7
        #   Line 4 WIRRAL, CH46 9PY             renders at y ≈ 66.3
        #   Line 5 Assessment Number + Full Rep renders at y ≈ 80.9
        #   Line 6 Version + Printed            renders at y ≈ 95.5
        # All in Verdana-Bold 12pt in real PDFs; we use Helvetica-Bold 12pt.
        # The specified y here = real y + small offset to account for baseline.
        c.setFont(FONT_BOLD, SIZE_HEADER)
        c.drawCentredString(PAGE_W/2, PAGE_H - 32.0, 'TEL: COLLISION ENGINEERS')
        c.drawCentredString(PAGE_W/2, PAGE_H - 46.6, '01515590762 77-79 HOYLAKE ROAD')
        c.drawCentredString(PAGE_W/2, PAGE_H - 61.2, 'MORETON')
        c.drawCentredString(PAGE_W/2, PAGE_H - 75.8, 'WIRRAL, CH46 9PY')
        c.setFont(FONT_BOLD, SIZE_SUBHDR)
        c.drawString(20.0,               PAGE_H - 90.4, f'Assessment Number: {self.assessment_number}')
        c.drawRightString(PAGE_W - 20.0, PAGE_H - 90.4, 'Full Report')
        c.drawString(20.0,               PAGE_H - 105.0, f'Version: {self.version}')
        c.drawRightString(PAGE_W - 20.0, PAGE_H - 105.0, f'Printed: {self.printed}')
        c.setFont(FONT_BOLD, SIZE_FOOTER)
        c.drawString(20.0, PAGE_H - 812.0, 'Audatex System Using Manufacturer Times')
        c.drawRightString(PAGE_W - 20.0, PAGE_H - 818.4,
                          f'PAGE {self.page_num} OF {{TOTAL_PAGES}}')

    # ── Page-break handling ─────────────────────────────────────────────────
    def _break_if_needed(self, needed_height):
        if self.y + needed_height > CONTENT_BOTTOM:
            self._new_page()

    def _new_page(self):
        self.c.showPage()
        self.page_num += 1
        self.y = CONTENT_TOP
        self._draw_chrome()
        if self.on_new_page is not None:
            self.on_new_page(self)

    # ── Public API ──────────────────────────────────────────────────────────
    def text(self, x, s, font=FONT, size=SIZE_BODY):
        """Draw single-line text at the current cursor y, advance minimally."""
        self._break_if_needed(size + 2)
        self.c.setFont(font, size)
        self.c.drawString(x, PAGE_H - self.y, s)

    def text_right(self, x_right, s, font=FONT, size=SIZE_BODY):
        self._break_if_needed(size + 2)
        self.c.setFont(font, size)
        self.c.drawRightString(x_right, PAGE_H - self.y, s)

    def text_center(self, x_center, s, font=FONT, size=SIZE_BODY):
        self._break_if_needed(size + 2)
        self.c.setFont(font, size)
        self.c.drawCentredString(x_center, PAGE_H - self.y, s)

    def advance(self, dy):
        self.y += dy

    def rule(self, x0=20.0, x1=None, thickness=0.5):
        if x1 is None:
            x1 = PAGE_W - 20.0
        self._break_if_needed(thickness + 1)
        self.c.setLineWidth(thickness)
        self.c.line(x0, PAGE_H - self.y, x1, PAGE_H - self.y)

    def space(self, dy):
        """Advance cursor by dy points."""
        self.y += dy

    def ensure_space(self, needed_height):
        """Break to a new page if less than needed_height remains."""
        self._break_if_needed(needed_height)

    def get_page_count(self):
        return self.page_num


def finalize_page_count(canvas_obj, total_pages, pdf_path):
    """
    After the PDF is written with placeholder {TOTAL_PAGES} markers,
    we re-render and substitute. For this project we take a simpler
    approach: draw page count during the rendering loop itself, and
    rely on a two-pass build (count pages, then re-render).

    See build_pdf() which orchestrates this.
    """
    pass  # Two-pass build handles this — see build_pdf()


# ─── Operation routing (from v3, slight additions) ───────────────────────────
def compile_assessment(raw):
    """
    Split operations into labour_rows / paint_rows / parts_rows / extras_rows.

    Operation dict shape:
      type:       one of the routing keys listed in the module docstring
      desc:       text to display
      guide:      guide number (e.g. '1481', '741', '1000', '752051')
      wu:         work units (numeric) — for labour/paint/specialist_wu
      price:      £ price (numeric) — for new_part / specialist_fixed
      part_num:   optional, for new_part
      unpriced:   optional bool, for new_part — if True, shows '*' asterisk
      bet:        optional betterment string (default '0%')
      continuations: optional list of extra description lines (INCLUDES: ...)
      text:       optional 'text tag' for extras (default 'Specialist')
    """
    labour_rows = []
    paint_rows  = []
    parts_rows  = []
    extras_rows = []

    rate = raw['rates']['labour_rate']

    for op in raw['operations']:
        t = op['type']

        if t == 'repair':
            desc = op.get('desc', '').upper()
            if not desc.startswith('REPAIR'):
                desc = f'REPAIR {desc}'
            lines = [desc] + list(op.get('continuations', []))
            labour_rows.append({
                'guide':      op.get('guide', ''),
                'desc_lines': lines,
                'wu':         f"{op['wu']:.1f}*",
            })

        elif t == 'rnr':
            lines = [op['desc'].upper()] + list(op.get('continuations', []))
            labour_rows.append({
                'guide':      op.get('guide', ''),
                'desc_lines': lines,
                'wu':         f"{op['wu']:.1f}",
            })

        elif t == 'check_labour':
            # "CHECK [panel]" labour line that lands in EVA's Check category
            desc = op.get('desc', '').upper()
            if not desc.startswith('CHECK'):
                desc = f'CHECK {desc}'
            lines = [desc] + list(op.get('continuations', []))
            labour_rows.append({
                'guide':      op.get('guide', ''),
                'desc_lines': lines,
                'wu':         f"{op['wu']:.1f}*",
            })

        elif t == 'paint_new':
            paint_rows.append({
                'guide': op.get('guide', ''),
                'desc':  f"{op['desc'].upper()} NEW PART PAINT K1R",
                'wu':    f"{op['wu']:.1f}",
            })

        elif t == 'paint_repair':
            paint_rows.append({
                'guide': op.get('guide', ''),
                'desc':  f"{op['desc'].upper()} REPAIR PAINTING <50%",
                'wu':    f"{op['wu']:.1f}",
            })

        elif t == 'paint_blend':
            paint_rows.append({
                'guide': op.get('guide', ''),
                'desc':  f"{op['desc'].upper()} SURFACE PAINT",
                'wu':    f"{op['wu']:.1f}",
            })

        elif t == 'paint_prep':
            paint_rows.append({
                'guide': op.get('guide', ''),
                'desc':  'PREPARATION FOR PRE-PAINTING',
                'wu':    f"{op['wu']:.1f}",
            })

        elif t == 'new_part':
            price_str = f"£{op['price']:,.2f}"
            if op.get('unpriced'):
                price_str += ' *'
            parts_rows.append({
                'guide':    op.get('guide', ''),
                'desc':     op['desc'].upper(),
                'part_num': op.get('part_num', ''),
                'bet':      op.get('bet', '0%'),
                'price':    price_str,
            })

        elif t == 'specialist_fixed':
            extras_rows.append({
                'desc':  op['desc'].upper(),
                'type':  op.get('text', 'Specialist'),
                'bet':   op.get('bet', '0%'),
                'price': f"£{op['price']:,.2f}",
            })

        elif t == 'specialist_wu':
            price_val = op['wu'] / 10.0 * rate
            extras_rows.append({
                'desc':  op['desc'].upper(),
                'type':  op.get('text', 'Specialist'),
                'bet':   op.get('bet', '0%'),
                'price': f"£{price_val:,.2f}",
            })

        else:
            raise ValueError(f"Unknown operation type: {t}")

    return {
        'labour_rows': labour_rows,
        'paint_rows':  paint_rows,
        'parts_rows':  parts_rows,
        'extras_rows': extras_rows,
    }


# ─── Section drawers — each knows how to render its own header + rows ───────
def draw_summary_and_vehicle(w, data):
    """Summary Information + Vehicle Details + Vehicle Condition (one shot)."""
    w.text(20.0, 'Summary Information', FONT_BOLD, SIZE_H2)
    w.advance(4)
    w.rule()
    w.advance(13)
    w.text(20.0, 'Claim', FONT_BOLD, SIZE_BODY)
    w.advance(13)

    claim_rows = [
        ('Authorisation Status:', 'Interim',                  'Date of Incident:',           'Not Known'),
        ('Work Provider:',        'OTHER',                    'Able to Authorise Repairs:',  'TBA'),
        ('Claim Reference:',      data.get('claim_ref', ''),  'Repairs Authorised?',         'TBA'),
        ('Policy Number:',        '',                         'VAT Portion Payable:',        'TBA'),
        ('Other Reference:',      '',                         'Repairer:',                   ''),
        ('Estimated Repair Time', '',                         '',                             ''),
        ('(Working Days):',       '',                         '',                             ''),
    ]
    for row in claim_rows:
        w.text(CLAIM_COL1_X, row[0])
        w.text(CLAIM_COL2_X, row[1])
        w.text(CLAIM_COL3_X, row[2])
        w.text(CLAIM_COL4_X, row[3])
        w.advance(11)

    w.advance(6)
    w.text(20.0, 'Vehicle Details', FONT_BOLD, SIZE_H2)
    w.advance(4)
    w.rule()
    w.advance(13)

    w.text(VEH_LABEL_X, 'Vehicle',     FONT_BOLD, SIZE_BODY)
    w.text(VEH_SPECS_X, 'Model Specs', FONT_BOLD, SIZE_BODY)
    w.advance(13)

    veh = data['vehicle']
    veh_rows = [
        ('Manufacturer:',        veh['manufacturer']),
        ('Model:',               veh['model']),
        ('Model Sheet Number:',  veh['model_sheet']),
        ('Engine:',              veh['engine']),
        ('Registration Number:', veh['reg']),
        ('VIN Number:',          veh['vin']),
        ('Registration Month:',  veh['reg_month']),
        ('Registration Year:',   veh['reg_year']),
        ('Odometer:',            veh.get('odometer', 'Not Known')),
        ('Colour:',              veh['colour']),
        ('Paint Code:',          veh['paint_code']),
        ('Build Date:',          veh['build_date']),
        ('Selection Type:',      veh.get('selection', 'AudaVIN+')),
        ('Fuel Type:',           veh['fuel']),
        ('Vehicle Imported:',    veh.get('imported', 'No')),
    ]
    # Interleave vehicle rows and specs rows — left vs right columns.
    # They're the same height so we can just render them row by row,
    # using fallback empty strings.
    specs = list(veh['specs'])
    n_rows = max(len(veh_rows), len(specs))
    for i in range(n_rows):
        if i < len(veh_rows):
            w.text(VEH_LABEL_X, veh_rows[i][0])
            w.text(VEH_VAL_X,   str(veh_rows[i][1]))
        if i < len(specs):
            w.text(VEH_SPECS_X, specs[i])
        w.advance(11)

    w.advance(6)
    w.text(20.0, 'Vehicle Condition', FONT_BOLD, SIZE_H2)
    w.advance(4)
    w.rule()
    w.advance(13)
    w.text(20.0, 'Vehicle Status', FONT_BOLD, SIZE_BODY)
    w.advance(13)

    cond_rows = [
        ('Pre-Accident Condition:', '', 'Severity of Impact:',           ''),
        ('Steering Rim Ply:',       '', 'Vehicle Status on Inspection:', ''),
        ('Brakes Pedal Travel:',    '', 'Date of Inspection:',           data.get('inspection_date', '')),
        ('Place of Inspection:',    '', '',                              ''),
        ('Pre-Accident Damage:',    '', '',                              ''),
    ]
    for row in cond_rows:
        w.text(CLAIM_COL1_X, row[0])
        w.text(CLAIM_COL2_X, row[1])
        w.text(CLAIM_COL3_X, row[2])
        w.text(CLAIM_COL4_X, row[3])
        w.advance(11)


def draw_tyres_and_damage(w, data):
    """Tyres Condition + Damage Areas block."""
    w.text(20.0, 'Tyres Condition:', FONT, SIZE_BODY)
    w.advance(11)
    w.text(27.0, 'Tread Depth LHF:', FONT, SIZE_BODY)
    w.text(CLAIM_COL3_X, 'Tread Depth RHF:', FONT, SIZE_BODY)
    w.advance(11)
    w.text(27.0, 'Tread Depth LHR:', FONT, SIZE_BODY)
    w.text(CLAIM_COL3_X, 'Tread Depth RHR:', FONT, SIZE_BODY)
    w.advance(15)
    w.text(20.0, 'Damage Areas:', FONT, SIZE_BODY)
    w.text(CLAIM_COL3_X, 'Direction of Impact:', FONT, SIZE_BODY)
    w.advance(20)


def draw_cost_summary(w, data, totals):
    """Addresses + Cost Summary (fixed block of rows)."""
    w.text(20.0, 'Addresses', FONT_BOLD, SIZE_H2)
    w.advance(4)
    w.rule()
    w.advance(22)
    w.text(21.0, 'No addresses entered.', FONT, SIZE_BODY)
    w.advance(24)
    w.text(20.0, 'Cost Summary', FONT_BOLD, SIZE_H2)
    w.advance(18)

    cost_rows = [
        ('Total Labour',         f"£{totals['total_labour']:,.2f}"),
        ('Total Paint/Material', f"£{totals['total_paint_material']:,.2f}"),
        ('Total Parts',          f"£{totals['total_parts']:,.2f}"),
        ('Additional Costs',     f"£{totals['total_extras']:,.2f}"),
        ('Grand Total Exc VAT:', f"£{totals['grand_ex_vat']:,.2f}"),
        ('20 % VAT:',            f"£{totals['vat']:,.2f}"),
        ('Grand Total Inc VAT:', f"£{totals['grand_inc_vat']:,.2f}"),
        ('Excess:',              'TBA'),
    ]
    for label, val in cost_rows:
        w.text(COST_LABEL_X, label, FONT, SIZE_BODY)
        w.text_right(COST_VAL_RIGHT, val, FONT, SIZE_BODY)
        w.advance(ROW_H_SUMMARY)


def labour_header(w, rate):
    """Draw the LABOUR section header + column headers. Reused on continuation pages."""
    w.advance(12)
    w.text(20.0, 'Repair Information', FONT_BOLD, SIZE_H2)
    w.advance(4)
    w.rule()
    w.advance(20)
    w.text(20.0, 'LABOUR', FONT_BOLD, SIZE_BODY)
    w.text(291.3, f'Time Basis 10 WU = 1 HR. Price = £{rate:.2f}/HR', FONT_BOLD, SIZE_BODY)
    w.advance(23)
    w.text(LAB_GUIDE_X, 'Repair / Guide', FONT_BOLD, SIZE_TABLE_HDR)
    w.advance(14)
    w.text(LAB_GUIDE_X, 'Number',         FONT_BOLD, SIZE_TABLE_HDR)
    w.text(LAB_DESC_X,  'Description',    FONT_BOLD, SIZE_TABLE_HDR)
    w.text_right(LAB_WU_RIGHT_X, 'Work Units', FONT_BOLD, SIZE_TABLE_HDR)
    w.advance(22)


def labour_continuation_header(w):
    """Real Audatex does not redraw any headers on continuation pages —
    it just continues the rows. EVA's parser expects this; adding a
    'LABOUR (continued)' header causes ghost rows to appear in EVA.
    So we intentionally do nothing here."""
    pass


def draw_labour(w, compiled, totals, rate):
    """Labour table, handles arbitrary row count with page breaks."""
    labour_header(w, rate)
    w.on_new_page = labour_continuation_header

    for item in compiled['labour_rows']:
        n_lines = len(item['desc_lines'])
        needed = n_lines * ROW_H_TABLE + 2
        w.ensure_space(needed)
        start_y = w.y
        w.text(LAB_GUIDE_X, item['guide'], FONT, SIZE_TABLE)
        for i, line in enumerate(item['desc_lines']):
            w.text(LAB_DESC_X, line, FONT, SIZE_TABLE)
            if i < n_lines - 1:
                w.advance(ROW_H_TABLE)
        if item.get('wu'):
            # WU goes on the first row of the item
            # Save y, move back to first line, draw, restore
            saved_y = w.y
            w.y = start_y
            w.text_right(LAB_WU_RIGHT_X, item['wu'], FONT, SIZE_TABLE)
            w.y = saved_y
        w.advance(ROW_H_TABLE)

    w.on_new_page = None

    # Totals — ensure they land on the same page as the last row if possible,
    # but since they're bold and small, let them break if needed.
    w.ensure_space(30)
    w.advance(12)
    w.text(258.8, 'Total Work Units', FONT_BOLD, SIZE_BODY)
    w.text_right(LAB_WU_RIGHT_X, f"{totals['labour_wu']:.1f}", FONT_BOLD, SIZE_BODY)
    w.advance(12)
    w.text(171.7, 'Total Panel / Mechanical Labour', FONT_BOLD, SIZE_BODY)
    w.text(412.3, f"{totals['labour_hours']:.2f} HRS", FONT_BOLD, SIZE_BODY)
    w.text_right(547.1, f"£{totals['labour_cost']:,.2f}", FONT_BOLD, SIZE_BODY)
    w.advance(18)
    w.text_center(PAGE_W/2, '*NOTE TIME BASIS = 10 WU / HOUR*', FONT, SIZE_TABLE)
    w.advance(11)
    w.text_center(PAGE_W/2,
                  '*OPINION TIMES ENTERED HAVE BEEN CONVERTED TO MATCH MANUFACTURERS TIMES*',
                  FONT, SIZE_TABLE)
    w.advance(20)


def paint_header(w, rate, coat_type):
    w.text(20.0, 'PAINT WORK', FONT_BOLD, SIZE_BODY)
    w.text(291.3, f'Time Basis 10 WU = 1 HR. Price = £{rate:.2f}/HR', FONT_BOLD, SIZE_BODY)
    w.advance(23)
    w.text(LAB_GUIDE_X, 'Repair /Guide',  FONT_BOLD, SIZE_TABLE_HDR)
    w.text(LAB_DESC_X,  'Description',    FONT_BOLD, SIZE_TABLE_HDR)
    w.advance(14)
    w.text(LAB_GUIDE_X, 'Number',         FONT_BOLD, SIZE_TABLE_HDR)
    w.text(LAB_DESC_X,  coat_type,        FONT_BOLD, SIZE_TABLE_HDR)
    w.text_right(LAB_WU_RIGHT_X, 'Work Units', FONT_BOLD, SIZE_TABLE_HDR)
    w.advance(22)


def paint_continuation_header(w):
    """No continuation header — real Audatex just continues rows."""
    pass


def draw_paint(w, compiled, totals, rate, coat_type):
    w.ensure_space(80)
    paint_header(w, rate, coat_type)
    w.on_new_page = paint_continuation_header

    for item in compiled['paint_rows']:
        w.ensure_space(ROW_H_TABLE + 2)
        w.text(LAB_GUIDE_X, item.get('guide', ''), FONT, SIZE_TABLE)
        w.text(LAB_DESC_X,  item.get('desc', ''),  FONT, SIZE_TABLE)
        if item.get('wu'):
            w.text_right(LAB_WU_RIGHT_X, item['wu'], FONT, SIZE_TABLE)
        w.advance(ROW_H_TABLE)

    w.on_new_page = None

    # Paint totals
    w.ensure_space(30)
    w.advance(12)
    w.text(258.8, 'Total Work Units', FONT_BOLD, SIZE_BODY)
    w.text_right(LAB_WU_RIGHT_X, f"{totals['paint_wu']:.1f}", FONT_BOLD, SIZE_BODY)
    w.advance(12)
    w.text(221.4, 'Total Paintwork Labour', FONT_BOLD, SIZE_BODY)
    w.text(415.0, f"{totals['paint_hours']:.1f} HRS.", FONT_BOLD, SIZE_BODY)
    w.text_right(547.1, f"£{totals['paint_cost']:,.2f}", FONT_BOLD, SIZE_BODY)
    w.advance(22)


def draw_paint_materials(w, totals):
    w.ensure_space(100)
    w.text(20.0, 'MATERIAL COST - PAINT', FONT_BOLD, SIZE_BODY)
    w.text_right(547.1, 'COST', FONT_BOLD, SIZE_BODY)
    w.advance(16)

    mat_rows = [
        ('Total Paint Cost',                    f"£{totals['paint_material_base']:,.2f}"),
        ('Sundry Paint Material',               f"£{totals['sundry_paint']:,.2f}"),
        ('Pre-Painting Sundry Materials',       f"£{totals['pre_sundry']:,.2f}"),
        ('Total Excluding Pearlescent Uplift',  f"£{totals['total_paint_material']:,.2f}"),
        ('Pearlescent Uplift @ 0.0%',           '£0.00'),
        ('Total Paint And Material Cost',       f"£{totals['total_paint_material']:,.2f}"),
    ]
    for i, (label, val) in enumerate(mat_rows):
        w.text(103.25, label, FONT, SIZE_BODY)
        w.text_right(547.1, val, FONT, SIZE_BODY)
        w.advance(11)
        if i == 2:
            w.advance(5)
        if i == 4:
            w.advance(5)
    w.advance(12)


def parts_header(w, price_valid):
    w.text(20.0, 'PARTS', FONT_BOLD, SIZE_BODY)
    w.advance(12)
    w.text_right(547.1, f"Price Valid: {price_valid}", FONT, SIZE_BODY)
    w.advance(22)
    w.text(PRT_GUIDE_X,   'Guide No.',   FONT_BOLD, SIZE_TABLE_HDR)
    w.text(PRT_DESC_X,    'Description', FONT_BOLD, SIZE_TABLE_HDR)
    w.text(PRT_PARTNUM_X, 'Part Number', FONT_BOLD, SIZE_TABLE_HDR)
    w.text(PRT_BET_X,     'Bet.',        FONT_BOLD, SIZE_TABLE_HDR)
    w.text_right(PRT_PRICE_RIGHT, 'Price', FONT_BOLD, SIZE_TABLE_HDR)
    w.advance(ROW_H_TABLE + 10)


def parts_continuation_header(w):
    """No continuation header — real Audatex just continues rows."""
    pass


def draw_parts(w, compiled, totals, sundry_pct, price_valid):
    w.ensure_space(60)
    parts_header(w, price_valid)
    w.on_new_page = parts_continuation_header

    # Parts Description column: from x=103.25 to x=242.00 = ~138pt wide
    MAX_PRT_DESC_WIDTH = PRT_PARTNUM_X - PRT_DESC_X - 4
    # Parts Part Number column: from x=242 to x=353 = ~111pt wide
    MAX_PRT_NUM_WIDTH = PRT_BET_X - PRT_PARTNUM_X - 4

    for item in compiled['parts_rows']:
        w.ensure_space(ROW_H_TABLE + 2)
        desc = item.get('desc', '')
        if stringWidth(desc, FONT, SIZE_TABLE) > MAX_PRT_DESC_WIDTH:
            while desc and stringWidth(desc + '...', FONT, SIZE_TABLE) > MAX_PRT_DESC_WIDTH:
                desc = desc[:-1]
            desc = desc.rstrip() + '...'
        part_num = item.get('part_num', '')
        if stringWidth(part_num, FONT, SIZE_TABLE) > MAX_PRT_NUM_WIDTH:
            while part_num and stringWidth(part_num + '...', FONT, SIZE_TABLE) > MAX_PRT_NUM_WIDTH:
                part_num = part_num[:-1]
            part_num = part_num.rstrip() + '...'
        w.text(PRT_GUIDE_X,   item.get('guide', ''),   FONT, SIZE_TABLE)
        w.text(PRT_DESC_X,    desc,                    FONT, SIZE_TABLE)
        w.text(PRT_PARTNUM_X, part_num,                FONT, SIZE_TABLE)
        w.text(PRT_BET_X,     item.get('bet', '0%'),   FONT, SIZE_TABLE)
        w.text_right(PRT_PRICE_RIGHT, item.get('price', ''), FONT, SIZE_TABLE)
        w.advance(ROW_H_TABLE)

    w.on_new_page = None

    # Totals
    w.ensure_space(60)
    if compiled['parts_rows']:
        w.advance(12)
        w.text(257.5, 'Sub Total', FONT, SIZE_BODY)
        w.text_right(547.1, f"£{totals['parts_subtotal']:,.2f}", FONT, SIZE_BODY)
        w.advance(11)
        w.text(257.5, 'Deduction from RRP', FONT, SIZE_BODY)
        w.text(412.0, '(0.0 %)', FONT, SIZE_BODY)
        w.text_right(547.1, '£0.00', FONT, SIZE_BODY)
        w.advance(11)
        w.text(257.5, 'Sundry Parts', FONT, SIZE_BODY)
        w.text(412.0, f"({sundry_pct} %)", FONT, SIZE_BODY)
        w.text_right(547.1, f"£{totals['parts_sundry']:,.2f}", FONT, SIZE_BODY)
        w.advance(11)
        w.text(384.0, 'Total Parts', FONT_BOLD, SIZE_BODY)
        w.text_right(547.1, f"£{totals['total_parts']:,.2f}", FONT_BOLD, SIZE_BODY)
    else:
        w.advance(12)
        w.text(257.5, 'Sub Total', FONT, SIZE_BODY)
        w.text_right(547.1, '£0.00', FONT, SIZE_BODY)
        w.advance(11)
        w.text(384.0, 'Total Parts', FONT_BOLD, SIZE_BODY)
        w.text_right(547.1, '£0.00', FONT_BOLD, SIZE_BODY)

    w.advance(18)
    w.text_center(PAGE_W/2, 'NB - COLOUR CODED ITEMS/TRIM - PART NUMBERS MAY DIFFER',
                  FONT, SIZE_TABLE)
    w.advance(22)


def extras_header(w):
    w.text(20.0, 'Extras', FONT_BOLD, SIZE_H2)
    w.advance(14)
    w.text(EXT_DESC_X, 'Description', FONT_BOLD, SIZE_TABLE_HDR)
    w.text(325.2,      'Betterment',  FONT_BOLD, SIZE_TABLE_HDR)
    w.text_right(EXT_PRICE_RIGHT, 'Price', FONT_BOLD, SIZE_TABLE_HDR)
    w.advance(ROW_H_TABLE)


def extras_continuation_header(w):
    """No continuation header — real Audatex just continues rows."""
    pass


def draw_extras(w, compiled, totals):
    w.ensure_space(40)
    extras_header(w)
    w.on_new_page = extras_continuation_header

    # Max width for description column in extras: from EXT_DESC_X (103.25) to EXT_SPEC_X (214.25)
    # = 111pt. At 7.5pt Helvetica, that's roughly 20-22 chars — leave a small gap.
    # We truncate with an ellipsis if too long.
    MAX_DESC_WIDTH = EXT_SPEC_X - EXT_DESC_X - 4  # leave 4pt gap before Specialist

    for item in compiled['extras_rows']:
        w.ensure_space(ROW_H_TABLE + 2)
        desc = item['desc']
        # Truncate if too wide
        desc_w = stringWidth(desc, FONT, SIZE_TABLE)
        if desc_w > MAX_DESC_WIDTH:
            # Binary-ish truncation with ellipsis
            while desc and stringWidth(desc + '...', FONT, SIZE_TABLE) > MAX_DESC_WIDTH:
                desc = desc[:-1]
            desc = desc.rstrip() + '...'
        w.text(EXT_DESC_X, desc, FONT, SIZE_TABLE)
        w.text(EXT_SPEC_X, item.get('type', 'Specialist'), FONT, SIZE_TABLE)
        w.text(EXT_BET_X,  item.get('bet', '0%'), FONT, SIZE_TABLE)
        w.text_right(EXT_PRICE_RIGHT, item['price'], FONT, SIZE_TABLE)
        w.advance(ROW_H_TABLE)

    w.on_new_page = None

    w.ensure_space(20)
    w.advance(5)
    w.text(EXT_BET_X, 'Total Extras', FONT_BOLD, SIZE_BODY)
    w.text_right(EXT_PRICE_RIGHT, f"£{totals['total_extras']:,.2f}", FONT_BOLD, SIZE_BODY)
    w.advance(22)


def draw_calculation(w, data, totals):
    w.ensure_space(300)
    w.text(20.0, 'Calculation', FONT_BOLD, SIZE_H2)
    w.text_right(PAGE_W - 20.0, data.get('calc_date', ''), FONT_BOLD, SIZE_H2)
    w.advance(4)
    w.rule()
    w.advance(16)

    w.text(20.0, 'Labour', FONT_BOLD, SIZE_BODY)
    w.advance(12)
    w.text(158.75, 'Total Panel/Mechanical', FONT, SIZE_BODY)
    w.text_right(440.0, f"£{totals['labour_cost']:,.2f}", FONT, SIZE_BODY)
    w.advance(11)
    w.text(158.75, 'Total Paintwork', FONT, SIZE_BODY)
    w.text_right(440.0, f"£{totals['paint_cost']:,.2f}", FONT, SIZE_BODY)
    w.advance(11)
    w.text(20.0, 'Total Labour', FONT_BOLD, SIZE_BODY)
    w.text_right(PAGE_W - 20.0, f"£{totals['total_labour']:,.2f}", FONT_BOLD, SIZE_BODY)
    w.advance(18)

    w.text(20.0, 'Total Paint/Material', FONT_BOLD, SIZE_BODY)
    w.text_right(PAGE_W - 20.0, f"£{totals['total_paint_material']:,.2f}", FONT_BOLD, SIZE_BODY)
    w.advance(11)
    w.text(20.0, 'Costs', FONT_BOLD, SIZE_BODY)
    w.advance(11)
    w.text(20.0, 'Total Parts', FONT_BOLD, SIZE_BODY)
    w.text_right(PAGE_W - 20.0, f"£{totals['total_parts']:,.2f}", FONT_BOLD, SIZE_BODY)
    w.advance(18)

    w.text(20.0, 'Additional Costs', FONT_BOLD, SIZE_BODY)
    w.advance(12)
    w.text(158.75, 'Cost of Specialist', FONT, SIZE_BODY)
    w.text_right(440.0, f"£{totals['total_extras']:,.2f}", FONT, SIZE_BODY)
    w.advance(11)
    w.text(20.0, 'Total Additional Costs', FONT_BOLD, SIZE_BODY)
    w.text_right(PAGE_W - 20.0, f"£{totals['total_extras']:,.2f}", FONT_BOLD, SIZE_BODY)
    w.advance(18)

    w.text(20.0, 'Grand Total Excl VAT', FONT_BOLD, SIZE_BODY)
    w.text_right(PAGE_W - 20.0, f"£{totals['grand_ex_vat']:,.2f}", FONT_BOLD, SIZE_BODY)
    w.advance(12)
    w.text(158.75, 'VAT @ 20 %', FONT, SIZE_BODY)
    w.text_right(440.0, f"£{totals['vat']:,.2f}", FONT, SIZE_BODY)
    w.advance(11)
    w.text(20.0, 'Grand Total Incl VAT', FONT_BOLD, SIZE_BODY)
    w.text_right(PAGE_W - 20.0, f"£{totals['grand_inc_vat']:,.2f}", FONT_BOLD, SIZE_BODY)
    w.advance(18)
    w.text(158.75, 'Excess', FONT, SIZE_BODY)
    w.text_right(440.0, 'TBA', FONT, SIZE_BODY)
    w.advance(22)

    w.text(20.0, 'Assessment Notes', FONT_BOLD, SIZE_H2)
    w.advance(12)

    notes = data.get('notes', 'No assessment notes entered.')
    max_chars = 100
    words = notes.split()
    line = ''
    for word in words:
        if len(line) + len(word) + 1 > max_chars:
            w.text(20.0, line, FONT, SIZE_BODY)
            w.advance(11)
            line = word
        else:
            line = f'{line} {word}' if line else word
    if line:
        w.text(20.0, line, FONT, SIZE_BODY)


# ─── Totals calculator (from v3) ─────────────────────────────────────────────
def compute_totals(compiled, data):
    rate = data['rates']['labour_rate']
    paint_rate = data['rates']['paint_rate']
    sundry_pct = data['rates']['sundry_parts_pct']
    sundry_paint = data['rates']['sundry_paint']
    pre_sundry = data['rates']['pre_sundry']
    paint_material_base = data['rates'].get('paint_material_base', 0.0)

    labour_wu = 0.0
    for r in compiled['labour_rows']:
        try:
            labour_wu += float(r['wu'].rstrip('*'))
        except (KeyError, ValueError, AttributeError):
            pass
    labour_hours = labour_wu / 10.0
    labour_cost  = labour_hours * rate

    paint_wu = 0.0
    for r in compiled['paint_rows']:
        try:
            paint_wu += float(r['wu'].rstrip('*'))
        except (KeyError, ValueError, AttributeError):
            pass
    paint_hours = paint_wu / 10.0
    paint_cost  = paint_hours * paint_rate

    total_labour = labour_cost + paint_cost
    total_paint_material = paint_material_base + sundry_paint + pre_sundry

    parts_subtotal = 0.0
    for r in compiled['parts_rows']:
        try:
            # Strip asterisk suffix if present, e.g. '£25.00 *'
            price_str = r['price'].replace('£', '').replace(',', '').rstrip('*').strip()
            parts_subtotal += float(price_str)
        except (KeyError, ValueError):
            pass
    parts_sundry = parts_subtotal * sundry_pct / 100.0 if parts_subtotal > 0 else 0.0
    total_parts  = parts_subtotal + parts_sundry

    total_extras = 0.0
    for r in compiled['extras_rows']:
        try:
            total_extras += float(r['price'].replace('£', '').replace(',', ''))
        except (KeyError, ValueError):
            pass

    grand_ex_vat = total_labour + total_paint_material + total_parts + total_extras
    vat = grand_ex_vat * 0.20
    grand_inc_vat = grand_ex_vat + vat

    return {
        'labour_wu':              labour_wu,
        'labour_hours':           labour_hours,
        'labour_cost':            labour_cost,
        'paint_wu':               paint_wu,
        'paint_hours':            paint_hours,
        'paint_cost':             paint_cost,
        'total_labour':           total_labour,
        'paint_material_base':    paint_material_base,
        'sundry_paint':           sundry_paint,
        'pre_sundry':             pre_sundry,
        'total_paint_material':   total_paint_material,
        'parts_subtotal':         parts_subtotal,
        'parts_sundry':           parts_sundry,
        'total_parts':            total_parts,
        'total_extras':           total_extras,
        'grand_ex_vat':           grand_ex_vat,
        'vat':                    vat,
        'grand_inc_vat':          grand_inc_vat,
    }


# ─── Build the PDF — two-pass approach for accurate page count ───────────────
def build_pdf(output_path, data):
    compiled = compile_assessment(data)
    totals = compute_totals(compiled, data)

    def render(output_target, placeholder_total='?'):
        """Render the full document. Returns the page count actually produced."""
        c = canvas.Canvas(output_target, pagesize=A4)
        c.setTitle(f'Audatex Estimate — {data["assessment_number"]}')
        c.setAuthor('Collision Engineers')

        w = PageWriter(c, data['assessment_number'], data['version'], data['printed'])

        # Pages flow naturally — PageWriter breaks when content doesn't fit.
        draw_summary_and_vehicle(w, data)
        draw_tyres_and_damage(w, data)
        draw_cost_summary(w, data, totals)
        draw_labour(w, compiled, totals, data['rates']['labour_rate'])
        draw_paint(w, compiled, totals, data['rates']['paint_rate'],
                   data.get('coat_type', 'BASECOAT CLEAR'))
        draw_paint_materials(w, totals)
        draw_parts(w, compiled, totals, data['rates']['sundry_parts_pct'],
                   data.get('price_valid', ''))
        draw_extras(w, compiled, totals)
        draw_calculation(w, data, totals)

        page_count = w.get_page_count()
        c.save()
        return page_count

    # Pass 1: render to a throwaway buffer to count pages
    buf = io.BytesIO()
    total_pages = render(buf)

    # Pass 2: we need to substitute the real page count into the page chrome.
    # Simplest approach: rewrite the build so it accepts the page count upfront.
    # We do this by monkey-patching PageWriter._draw_chrome to use the real total.
    # Re-render to actual output file.
    original_draw_chrome = PageWriter._draw_chrome
    def patched_draw_chrome(self):
        c = self.c
        c.setFont(FONT_BOLD, SIZE_HEADER)
        c.drawCentredString(PAGE_W/2, PAGE_H - 32.0, 'TEL: COLLISION ENGINEERS')
        c.drawCentredString(PAGE_W/2, PAGE_H - 46.6, '01515590762 77-79 HOYLAKE ROAD')
        c.drawCentredString(PAGE_W/2, PAGE_H - 61.2, 'MORETON')
        c.drawCentredString(PAGE_W/2, PAGE_H - 75.8, 'WIRRAL, CH46 9PY')
        c.setFont(FONT_BOLD, SIZE_SUBHDR)
        c.drawString(20.0,               PAGE_H - 90.4, f'Assessment Number: {self.assessment_number}')
        c.drawRightString(PAGE_W - 20.0, PAGE_H - 90.4, 'Full Report')
        c.drawString(20.0,               PAGE_H - 105.0, f'Version: {self.version}')
        c.drawRightString(PAGE_W - 20.0, PAGE_H - 105.0, f'Printed: {self.printed}')
        c.setFont(FONT_BOLD, SIZE_FOOTER)
        c.drawString(20.0, PAGE_H - 812.0, 'Audatex System Using Manufacturer Times')
        c.drawRightString(PAGE_W - 20.0, PAGE_H - 818.4,
                          f'PAGE {self.page_num} OF {total_pages}')

    PageWriter._draw_chrome = patched_draw_chrome
    try:
        render(output_path)
    finally:
        PageWriter._draw_chrome = original_draw_chrome

    print(f'PDF generated: {output_path}  ({total_pages} pages)')
    return {'compiled': compiled, 'totals': totals, 'total_pages': total_pages}


# ─── Test: reconstruct the 82-line Vauxhall Vivaro test job ──────────────────
if __name__ == '__main__':
    data = {
        'assessment_number': 'AI000004',
        'version':           'AI/VX15VZH/4',
        'printed':           '23/04/2026',
        'calc_date':         '23/04/2026',
        'price_valid':       '23/04/2026',
        'claim_ref':         'TEST',
        'inspection_date':   '23/04/2026',
        'coat_type':         'TWO COAT METALLIC',

        'rates': {
            'labour_rate':         80.00,
            'paint_rate':          80.00,
            'sundry_parts_pct':    3.5,
            'sundry_paint':        120.16,
            'pre_sundry':           0.00,
            'paint_material_base': 4408.50,
        },

        'vehicle': {
            'manufacturer': 'VAUXHALL',
            'model':        'VIVARO Base Model',
            'model_sheet':  '592',
            'engine':       '1.6 LTR 84/5/8/9 KW',
            'reg':          'VX15VZH',
            'vin':          'W0L3F7018FV619528',
            'reg_month':    'March',
            'reg_year':     '2015',
            'colour':       'GREY',
            'paint_code':   '10H',
            'build_date':   'FROM 02/2015',
            'fuel':         'Diesel',
            'specs': [
                'FROM 02/2015', 'AIR CONDITIONING', 'ELIMINATE INN MIRROR',
                'C-LOCKING W/DEADLOCK', 'F-REGULATOR COMFORT',
                'ELECT/HEAT D/MIRROR', 'RADIO CD 18 BT',
                'DIGITAL RADIOSYSTEM', 'FOG LAMPS',
                'DAYTIME RUN LIGH LED', 'MULTIFUNC S/CUSHION',
                'SEAT CLOTH CONNECT', 'IN TRIM SATIN CHROME',
                'PARTITION PANEL', 'CHROME MOULDING',
                'PARK PILOT SYSTEM', '1.6 LTR 84/5/8/9 KW',
                'EMISSION STD EURO 5', 'GEARBOX 6 SPEED',
                'LEATHER STRG WHEEL', 'CRUISE CONTROL',
                'TYRES 205/65 R16C', 'WHEELS 6J X 16',
                'FULL WHEEL COVERS', 'SPARE WHEEL STEEL',
                'GWR 2900 KG', 'L/SLIDING DOOR', 'REAR WING DOORS',
                'FLAT ROOF', 'WHEELBASE 3498 MM', 'FACTORY LUTON',
                'VAN', 'TWO COAT METALLIC',
            ],
        },

        # ─── Operations — mirroring the 82-line Audatex test job ───
        'operations': [

            # === LABOUR / R+R operations ===
            {'type': 'rnr',   'guide': 'NO NUMBER', 'wu': 2.0,
             'desc': 'ALIGN BODY BY HANDHELD MEASURING SYSTEM'},
            {'type': 'rnr',   'guide': '0110702',   'wu': 7.0, 'desc': 'R + R FRONT BUMPER'},
            {'type': 'rnr',   'guide': '1420120019','wu': 1.0, 'desc': 'R + R FRONT BUMPER IMPACT DAMPER'},
            {'type': 'rnr',   'guide': '2040510',   'wu': 2.0, 'desc': 'R + R LEFT HEADLAMP'},
            {'type': 'rnr',   'guide': '2040510',   'wu': 2.0, 'desc': 'R + R RIGHT HEADLAMP'},
            {'type': 'rnr',   'guide': '2040512',   'wu': 1.0, 'desc': 'ADJUST HEADLAMPS'},
            {'type': 'rnr',   'guide': '1410050',   'wu': 13.0, 'desc': 'RENEW BONNET',
             'continuations': ['INCLUDES: R + R ENGINE BONNET', 'STRIP + REFIT']},
            {'type': 'rnr',   'guide': '2020380',   'wu': 18.0, 'desc': 'R + R WINDSCREEN',
             'continuations': ['INCLUDES: R + R COWL TRIM, WIPER ARMS', 'AND A-PILLAR TRIMS']},
            {'type': 'rnr',   'guide': '0111540',   'wu': 157.0, 'desc': 'RENEW REAR ROOF'},
            {'type': 'rnr',   'guide': '0111540032','wu': 21.0, 'desc': 'RENEW REAR ROOF FRAME'},
            {'type': 'rnr',   'guide': '1042980',   'wu': 5.0, 'desc': 'R + R LEFT FRONT DOOR TRIM'},
            {'type': 'rnr',   'guide': '0111012',   'wu': 9.0, 'desc': 'R + R LEFT SLIDING DOOR'},
            {'type': 'rnr',   'guide': '1412510',   'wu': 18.0, 'desc': 'RENEW L/SLIDING DOOR (REMOVED)'},
            {'type': 'rnr',   'guide': '2041490',   'wu': 2.0, 'desc': 'R + R LEFT TAIL LAMP'},
            {'type': 'rnr',   'guide': '2041490',   'wu': 2.0, 'desc': 'R + R RIGHT TAIL LAMP'},
            {'type': 'rnr',   'guide': '0110732',   'wu': 7.0, 'desc': 'R + R REAR BUMPER CPL'},
            {'type': 'rnr',   'guide': 'NO NUMBER', 'wu': 1.0, 'desc': 'R + R REAR REFLECTOR'},
            {'type': 'rnr',   'guide': '1016210',   'wu': 1.0, 'desc': 'R + R L/R ROOF PILLAR'},
            {'type': 'rnr',   'guide': '0100910',   'wu': 159.0, 'desc': 'RENEW R/R SIDE PANEL',
             'continuations': ['INCLUDES: TRIMS AND ATTACHED PARTS REMOVE AND REFIT']},
            {'type': 'rnr',   'guide': '1044260',   'wu': 2.0, 'desc': 'R + R LEFT LOADING DOOR TRIM'},
            {'type': 'rnr',   'guide': '1415730',   'wu': 1.0, 'desc': 'R + R LEFT REAR DOOR CHECK'},
            {'type': 'rnr',   'guide': '1041280',   'wu': 3.0, 'desc': 'R + R L/UPPER B-PILLAR TRIM'},
            {'type': 'rnr',   'guide': '1041280',   'wu': 3.0, 'desc': 'R + R R/UPPER B-PILLAR TRIM'},
            {'type': 'rnr',   'guide': '1020110',   'wu': 2.0, 'desc': 'R + R FRONT HEADLINING'},
            {'type': 'rnr',   'guide': '1415840',   'wu': 9.0, 'desc': 'R + R LOWER LOAD COMPARTMENT PARTITION'},
            {'type': 'rnr',   'guide': '0801752',   'wu': 5.0, 'desc': 'R + R L/F SEAT CUSHION'},
            {'type': 'rnr',   'guide': '0801742',   'wu': 6.0, 'desc': 'R + R RIGHT FRONT SEAT'},
            {'type': 'rnr',   'guide': '6420220',   'wu': 5.0, 'desc': 'R + R PARKING HELP CONTROL UNIT'},
            {'type': 'rnr',   'guide': '0801440',   'wu': 1.0, 'desc': 'R + R RIGHT REAR WHEEL'},
            {'type': 'rnr',   'guide': '0801440',   'wu': 1.0, 'desc': 'R + R WHEEL/S (ADD/WORK)'},

            # === REPAIRS ===
            {'type': 'rnr',   'guide': '1509',     'wu': 1.0, 'desc': 'REMOVE&REFIT L/F DOOR SEAL'},
            {'type': 'repair','guide': '1481',     'wu': 15.0, 'desc': 'LEFT FRONT DOOR'},
            {'type': 'repair','guide': '2085',     'wu': 50.0, 'desc': 'L/F DOOR FRAME'},
            {'type': 'repair','guide': '3744',     'wu': 5.0,  'desc': 'R/R UPPER SIDE PANEL'},

            # === SPECIALIST labour-style (lands in R&R) ===
            {'type': 'rnr', 'guide': '1000', 'wu': 5.0,  'desc': 'Specialist TRIAL PANEL FIT'},

            # === CHECK labour (lands in EVA Check column) ===
            {'type': 'check_labour', 'guide': '1737', 'wu': 2.0, 'desc': 'L/DOOR MIRROR'},

            # === Additional R+R "specialist" labour (lands in R&R) ===
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist QC AND ROAD TEST'},
            {'type': 'rnr', 'guide': '1000', 'wu': 5.0,  'desc': 'Specialist PRE REPAIR CLEAN'},
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist WASH AND VACUUM'},
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'work test description'},
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist QC AND ROAD TEST'},
            {'type': 'rnr', 'guide': '1000', 'wu': 5.0,  'desc': 'Specialist PRE REPAIR CLEAN'},
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist POST REPAIR CHECK'},
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist PRE REPAIR CHECK'},
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist WASH AND VACUUM'},
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist STANDARD SHUTDOWN'},
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist SPECIALIST VALET'},
            {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist YARD CHARGE'},
            {'type': 'rnr', 'guide': '1000', 'wu': 1.0,  'desc': 'Corrosion Prote ADDITIONAL PANEL'},

            # === PAINT ===
            {'type': 'paint_blend', 'guide': '221',  'wu': 170.0, 'desc': 'OUTER BODY'},
            {'type': 'paint_new',   'guide': '1000', 'wu': 3.0,   'desc': 'DENIB / POLISH'},
            {'type': 'paint_blend', 'guide': '1000', 'wu': 5.0,   'desc': 'MASKING TIME ADDIITONAL'},
            {'type': 'paint_prep',  'guide': '',     'wu': 13.0},

            # === PARTS ===
            {'type': 'new_part', 'guide': '349',  'desc': 'FRT IMPACT DAMPER',  'part_num': '93450026',   'price':   52.78},
            {'type': 'new_part', 'guide': '410',  'desc': 'GRILLE',             'part_num': '93450928',   'price':  348.16},
            {'type': 'new_part', 'guide': '471',  'desc': 'BONNET',             'part_num': '6500694680', 'price':  642.42},
            {'type': 'new_part', 'guide': '562',  'desc': 'RIGHT HEADLAMP ASSY','part_num': '95527871',   'price':  460.36},
            {'type': 'new_part', 'guide': '1411', 'desc': 'WINDSCREEN BOND KIT','part_num': '93165025',   'price':   94.58},
            {'type': 'new_part', 'guide': '1729', 'desc': 'L/F MOUNTING KIT',   'part_num': 'USE SINGLE PARTS', 'price': 0.00},
            {'type': 'new_part', 'guide': '1731', 'desc': 'CLIP FASTENING',     'part_num': '95508766',   'price':   24.48},
            {'type': 'new_part', 'guide': '1733', 'desc': 'SCREW',              'part_num': '93452165',   'price':    6.20},
            {'type': 'new_part', 'guide': '1735', 'desc': 'SCREW',              'part_num': '91169531',   'price':    2.88},
            {'type': 'new_part', 'guide': '1781', 'desc': 'L/SLIDING DOOR',     'part_num': '93455820',   'price':  815.05},
            {'type': 'new_part', 'guide': '2353', 'desc': 'REAR ROOF',          'part_num': '95518334',   'price': 2224.99},
            {'type': 'new_part', 'guide': '2357', 'desc': 'REAR ROOF FRAME',    'part_num': '91160066',   'price':  247.67},
            {'type': 'new_part', 'guide': '2392', 'desc': 'RR ROOF REPAIR KIT', 'part_num': '1699686780', 'price':   73.51},
            {'type': 'new_part', 'guide': '2963', 'desc': 'L/R LOWER DOOR STOP','part_num': '93850442',   'price':   14.96},
            {'type': 'new_part', 'guide': '3297', 'desc': 'L/R REFLECTOR',      'part_num': '9160858',    'price':   13.87},
            {'type': 'new_part', 'guide': '3482', 'desc': 'R/R SIDE PANEL',     'part_num': '95518700',   'price': 1743.51},
            {'type': 'new_part', 'guide': '9637', 'desc': 'PARK SYS CONT UNIT', 'part_num': '93868062',   'price':   83.09},
            {'type': 'new_part', 'guide': '1000', 'desc': 'BEAD SEALER',        'part_num': 'Renew',      'price':   25.00, 'unpriced': True},
            {'type': 'new_part', 'guide': '1000', 'desc': 'BORON DRILLS (8MM)', 'part_num': 'Renew',      'price':   72.87, 'unpriced': True},
            {'type': 'new_part', 'guide': '1000', 'desc': 'COOLANT / ATF',      'part_num': 'Renew',      'price':   25.00, 'unpriced': True},

            # === EXTRAS ===
            {'type': 'specialist_fixed', 'desc': 'ASSESSMENT FEE',                     'price': 176.96},
            {'type': 'specialist_fixed', 'desc': 'VEHICLE CARE KIT',                   'price':  10.41},
            {'type': 'specialist_fixed', 'desc': 'WHEEL ALIGNMENT', 'text': 'Check & Adj toe', 'price': 174.88},
            {'type': 'specialist_fixed', 'desc': 'CORROSION PROTECTION MATERIALS EXTERNAL', 'price': 7.29},
        ],

        'notes': (
            'Comprehensive damage assessment. Vehicle has suffered severe impact damage '
            'across multiple panels. Full repair specification compiled. '
            'AI CONFIDENCE: MEDIUM. Subject to engineer review and approval.'
        ),
    }

    result = build_pdf('/home/claude/work/ours_v4.pdf', data)
    print()
    print('=== Compiled sections ===')
    for section, rows in result['compiled'].items():
        print(f'  {section}: {len(rows)} rows')
    print()
    print('=== Key totals ===')
    t = result['totals']
    print(f"  Labour WU:            {t['labour_wu']:.1f}  ({t['labour_hours']:.2f} hrs)")
    print(f"  Labour cost:          £{t['labour_cost']:,.2f}")
    print(f"  Paint WU:             {t['paint_wu']:.1f}   ({t['paint_hours']:.2f} hrs)")
    print(f"  Paint cost:           £{t['paint_cost']:,.2f}")
    print(f"  Total labour:         £{t['total_labour']:,.2f}")
    print(f"  Total paint material: £{t['total_paint_material']:,.2f}")
    print(f"  Total parts:          £{t['total_parts']:,.2f}")
    print(f"  Total extras:         £{t['total_extras']:,.2f}")
    print(f"  Grand ex VAT:         £{t['grand_ex_vat']:,.2f}")
    print(f"  Grand inc VAT:        £{t['grand_inc_vat']:,.2f}")
    print(f"  Total pages:          {result['total_pages']}")