NOTE: This is a .py converted to markdown for ease of opening and viewing.



"""

Audatex-style PDF generator — v4 (dynamic pagination, routing-aware).

&#x20;

IMPORTANT NOTE FOR FUTURE CLAUDE:

In EVA, the last row of the Parts screen is ALWAYS an empty row with Type

dropdown set to R\&R — it's EVA's manual-entry row, allowing the engineer

to click and add a custom line. It is NOT a ghost row from our PDF parser.

Do not try to "fix" it.

&#x20;

During development, some rows appeared to contain text like "Zh/4" or "/4"

which I misread as fragmented version-string text. On closer inspection,

these were either the manual-entry row or highlighting artefacts in the

screenshots. The PDF parser is working correctly.

&#x20;

Changes from `Later`/`unallocated`:

&#x20; - Dynamic pagination via PageWriter — tables flow across any number of pages

&#x20; - Removed STANDARD auto-inclusion (AI is now responsible for full operations list)

&#x20; - No continuation-page headers on tables (real Audatex doesn't use them)

&#x20; - Font sizes match real Audatex (Verdana 12pt chrome, 10pt headers, 9pt body)

&#x20; - Chrome y-positions match real Audatex (22, 37, 52, 66, 81, 95)

&#x20; - CONTENT\_TOP=188, CONTENT\_BOTTOM=750 for generous parser safe zones

&#x20; - Two-pass build for accurate "PAGE X OF Y"

&#x20; - Description truncation with ellipsis for long descriptions

&#x20; - Asterisk markers on unpriced parts

&#x20;

Rulebook (unchanged from `Later`/`unallocated`):

&#x20; Operation type      → PDF section        → EVA Type     → Engineer's Report

&#x20; ──────────────────────────────────────────────────────────────────────────

&#x20; new\_part            → Parts              → New          → Main new parts

&#x20; repair              → Labour (w/ REPAIR) → Repair       → Repairs

&#x20; rnr                 → Labour             → R\&R          → (hidden)

&#x20; check\_labour        → Labour (w/ CHECK)  → Check        → Additional ops

&#x20; paint\_new           → Paint (NEW PART)   → Paint        → Additional ops

&#x20; paint\_repair        → Paint (REPAIR)     → Paint        → Additional ops

&#x20; paint\_blend         → Paint (SURFACE)    → Blend        → Additional ops

&#x20; paint\_prep          → Paint (PREP)       → Paint        → Additional ops

&#x20; specialist\_fixed    → Extras             → Specialist   → Additional ops

&#x20; specialist\_wu       → Extras (£=WU×rate) → Specialist   → Additional ops

&#x20;

Coordinate positions (verified across 5 real Audatex PDFs):

&#x20; Labour:  Guide x=20.0    Desc x=158.75   WU right x=547.15

&#x20; Parts:   Guide x=20.0    Desc x=103.25   PartNum x=242.0   Bet x=353.0   Price right x=547.15

&#x20; Extras:  Desc x=103.25   Specialist x=214.25   Price right x=547.15

&#x20; Cost:    Labels x=348.0  Values right x=549.7

"""

&#x20;

import io

from reportlab.pdfgen import canvas

from reportlab.lib.pagesizes import A4

from reportlab.pdfbase import pdfmetrics

from reportlab.pdfbase.pdfmetrics import stringWidth

&#x20;

PAGE\_W, PAGE\_H = A4  # 595.276 x 841.89

&#x20;

\# ─── Anchored coordinates ────────────────────────────────────────────────────

LAB\_GUIDE\_X      = 20.0

LAB\_DESC\_X       = 158.75

LAB\_WU\_RIGHT\_X   = 547.15

&#x20;

PRT\_GUIDE\_X      = 20.0

PRT\_DESC\_X       = 103.25

PRT\_PARTNUM\_X    = 242.0

PRT\_BET\_X        = 353.0

PRT\_PRICE\_RIGHT  = 547.15

&#x20;

EXT\_DESC\_X       = 103.25

EXT\_SPEC\_X       = 214.25

EXT\_BET\_X        = 325.2

EXT\_PRICE\_RIGHT  = 547.15

&#x20;

COST\_LABEL\_X     = 348.0

COST\_VAL\_RIGHT   = 549.7

&#x20;

VEH\_LABEL\_X      = 20.0

VEH\_VAL\_X        = 158.75

VEH\_SPECS\_X      = 325.0

&#x20;

CLAIM\_COL1\_X     = 20.0

CLAIM\_COL2\_X     = 158.75

CLAIM\_COL3\_X     = 325.0

CLAIM\_COL4\_X     = 463.0

&#x20;

ROW\_H\_TABLE      = 12.14

ROW\_H\_SUMMARY    = 10.9

&#x20;

FONT            = 'Helvetica'

FONT\_BOLD       = 'Helvetica-Bold'

&#x20;

\# Font sizes — measured from real Audatex PDFs (Verdana-Bold 12pt chrome,

\# Verdana-Bold 10pt section headers, Verdana 9pt body, Verdana-Bold 10pt WU values).

\# We use Helvetica instead of Verdana; EVA doesn't care about the font face,

\# only about the layout. Matching sizes keeps us within EVA's parser expectations.

SIZE\_HEADER     = 12          # Company block (TEL: COLLISION ENGINEERS etc.)

SIZE\_SUBHDR     = 12          # Assessment Number, Version, etc.

SIZE\_H2         = 12          # Section headers (Cost Summary, Addresses, etc.)

SIZE\_BODY       = 10          # LABOUR / PARTS / Extras section labels

SIZE\_TABLE\_HDR  = 10          # Column headers (Number, Description, Work Units)

SIZE\_TABLE      = 9           # Table data rows

SIZE\_WU\_BOLD    = 10          # Work Unit values (bold right-aligned)

SIZE\_FOOTER     = 9

&#x20;

\# y-coordinates of usable content area.

\# Real Audatex PDFs always start content at y≈188 on every page.

\# EVA's parser treats y<\~170 as the page-chrome safe zone — any text in that

\# band can be misread as a ghost row. So we match the real Audatex convention

\# and leave a \~90-point gap below the chrome for the parser's safety.

CONTENT\_TOP     = 188.0

\# Real Audatex stops content around y=750, leaving \~55pt of blank space above

\# the footer. If we fill too close to the footer, EVA's parser gets confused

\# about where the table ends and where the page chrome of the next page begins.

CONTENT\_BOTTOM  = 750.0

&#x20;

&#x20;

\# ─── PageWriter — handles pagination automatically ───────────────────────────

class PageWriter:

&#x20;   """

&#x20;   Wraps a reportlab canvas and tracks the current y-cursor.

&#x20;

&#x20;   Callers use emit\_row() / emit\_rule() / emit\_space() to add content;

&#x20;   the writer breaks to a new page automatically when the cursor would

&#x20;   spill past CONTENT\_BOTTOM.

&#x20;

&#x20;   On each new page, it redraws page chrome and invokes an optional

&#x20;   'on\_new\_page' callback (used to redraw table headers).

&#x20;   """

&#x20;

&#x20;   def \_\_init\_\_(self, canvas\_obj, assessment\_number, version, printed):

&#x20;       self.c = canvas\_obj

&#x20;       self.assessment\_number = assessment\_number

&#x20;       self.version = version

&#x20;       self.printed = printed

&#x20;       self.y = CONTENT\_TOP

&#x20;       self.page\_num = 1

&#x20;       self.on\_new\_page = None   # callback for continuation headers

&#x20;       self.\_draw\_chrome()

&#x20;

&#x20;   # ── Low-level drawing ───────────────────────────────────────────────────

&#x20;   def \_draw\_chrome(self):

&#x20;       c = self.c

&#x20;       # Chrome y-positions match real Audatex exactly (measured from TEST\_3

&#x20;       # after accounting for ReportLab's baseline rendering of 12pt).

&#x20;       #   Line 1 TEL: COLLISION ENGINEERS     renders at y ≈ 22.5

&#x20;       #   Line 2 phone + address              renders at y ≈ 37.1

&#x20;       #   Line 3 MORETON                      renders at y ≈ 51.7

&#x20;       #   Line 4 WIRRAL, CH46 9PY             renders at y ≈ 66.3

&#x20;       #   Line 5 Assessment Number + Full Rep renders at y ≈ 80.9

&#x20;       #   Line 6 Version + Printed            renders at y ≈ 95.5

&#x20;       # All in Verdana-Bold 12pt in real PDFs; we use Helvetica-Bold 12pt.

&#x20;       # The specified y here = real y + small offset to account for baseline.

&#x20;       c.setFont(FONT\_BOLD, SIZE\_HEADER)

&#x20;       c.drawCentredString(PAGE\_W/2, PAGE\_H - 32.0, 'TEL: COLLISION ENGINEERS')

&#x20;       c.drawCentredString(PAGE\_W/2, PAGE\_H - 46.6, '01515590762 77-79 HOYLAKE ROAD')

&#x20;       c.drawCentredString(PAGE\_W/2, PAGE\_H - 61.2, 'MORETON')

&#x20;       c.drawCentredString(PAGE\_W/2, PAGE\_H - 75.8, 'WIRRAL, CH46 9PY')

&#x20;       c.setFont(FONT\_BOLD, SIZE\_SUBHDR)

&#x20;       c.drawString(20.0,               PAGE\_H - 90.4, f'Assessment Number: {self.assessment\_number}')

&#x20;       c.drawRightString(PAGE\_W - 20.0, PAGE\_H - 90.4, 'Full Report')

&#x20;       c.drawString(20.0,               PAGE\_H - 105.0, f'Version: {self.version}')

&#x20;       c.drawRightString(PAGE\_W - 20.0, PAGE\_H - 105.0, f'Printed: {self.printed}')

&#x20;       c.setFont(FONT\_BOLD, SIZE\_FOOTER)

&#x20;       c.drawString(20.0, PAGE\_H - 812.0, 'Audatex System Using Manufacturer Times')

&#x20;       c.drawRightString(PAGE\_W - 20.0, PAGE\_H - 818.4,

&#x20;                         f'PAGE {self.page\_num} OF {{TOTAL\_PAGES}}')

&#x20;

&#x20;   # ── Page-break handling ─────────────────────────────────────────────────

&#x20;   def \_break\_if\_needed(self, needed\_height):

&#x20;       if self.y + needed\_height > CONTENT\_BOTTOM:

&#x20;           self.\_new\_page()

&#x20;

&#x20;   def \_new\_page(self):

&#x20;       self.c.showPage()

&#x20;       self.page\_num += 1

&#x20;       self.y = CONTENT\_TOP

&#x20;       self.\_draw\_chrome()

&#x20;       if self.on\_new\_page is not None:

&#x20;           self.on\_new\_page(self)

&#x20;

&#x20;   # ── Public API ──────────────────────────────────────────────────────────

&#x20;   def text(self, x, s, font=FONT, size=SIZE\_BODY):

&#x20;       """Draw single-line text at the current cursor y, advance minimally."""

&#x20;       self.\_break\_if\_needed(size + 2)

&#x20;       self.c.setFont(font, size)

&#x20;       self.c.drawString(x, PAGE\_H - self.y, s)

&#x20;

&#x20;   def text\_right(self, x\_right, s, font=FONT, size=SIZE\_BODY):

&#x20;       self.\_break\_if\_needed(size + 2)

&#x20;       self.c.setFont(font, size)

&#x20;       self.c.drawRightString(x\_right, PAGE\_H - self.y, s)

&#x20;

&#x20;   def text\_center(self, x\_center, s, font=FONT, size=SIZE\_BODY):

&#x20;       self.\_break\_if\_needed(size + 2)

&#x20;       self.c.setFont(font, size)

&#x20;       self.c.drawCentredString(x\_center, PAGE\_H - self.y, s)

&#x20;

&#x20;   def advance(self, dy):

&#x20;       self.y += dy

&#x20;

&#x20;   def rule(self, x0=20.0, x1=None, thickness=0.5):

&#x20;       if x1 is None:

&#x20;           x1 = PAGE\_W - 20.0

&#x20;       self.\_break\_if\_needed(thickness + 1)

&#x20;       self.c.setLineWidth(thickness)

&#x20;       self.c.line(x0, PAGE\_H - self.y, x1, PAGE\_H - self.y)

&#x20;

&#x20;   def space(self, dy):

&#x20;       """Advance cursor by dy points."""

&#x20;       self.y += dy

&#x20;

&#x20;   def ensure\_space(self, needed\_height):

&#x20;       """Break to a new page if less than needed\_height remains."""

&#x20;       self.\_break\_if\_needed(needed\_height)

&#x20;

&#x20;   def get\_page\_count(self):

&#x20;       return self.page\_num

&#x20;

&#x20;

def finalize\_page\_count(canvas\_obj, total\_pages, pdf\_path):

&#x20;   """

&#x20;   After the PDF is written with placeholder {TOTAL\_PAGES} markers,

&#x20;   we re-render and substitute. For this project we take a simpler

&#x20;   approach: draw page count during the rendering loop itself, and

&#x20;   rely on a two-pass build (count pages, then re-render).

&#x20;

&#x20;   See build\_pdf() which orchestrates this.

&#x20;   """

&#x20;   pass  # Two-pass build handles this — see build\_pdf()

&#x20;

&#x20;

\# ─── Operation routing (from `Later`/`unallocated`, slight additions) ───────────────────────────

def compile\_assessment(raw):

&#x20;   """

&#x20;   Split operations into labour\_rows / paint\_rows / parts\_rows / extras\_rows.

&#x20;

&#x20;   Operation dict shape:

&#x20;     type:       one of the routing keys listed in the module docstring

&#x20;     desc:       text to display

&#x20;     guide:      guide number (e.g. '1481', '741', '1000', '752051')

&#x20;     wu:         work units (numeric) — for labour/paint/specialist\_wu

&#x20;     price:      £ price (numeric) — for new\_part / specialist\_fixed

&#x20;     part\_num:   optional, for new\_part

&#x20;     unpriced:   optional bool, for new\_part — if True, shows '\*' asterisk

&#x20;     bet:        optional betterment string (default '0%')

&#x20;     continuations: optional list of extra description lines (INCLUDES: ...)

&#x20;     text:       optional 'text tag' for extras (default 'Specialist')

&#x20;   """

&#x20;   labour\_rows = \[]

&#x20;   paint\_rows  = \[]

&#x20;   parts\_rows  = \[]

&#x20;   extras\_rows = \[]

&#x20;

&#x20;   rate = raw\['rates']\['labour\_rate']

&#x20;

&#x20;   for op in raw\['operations']:

&#x20;       t = op\['type']

&#x20;

&#x20;       if t == 'repair':

&#x20;           desc = op.get('desc', '').upper()

&#x20;           if not desc.startswith('REPAIR'):

&#x20;               desc = f'REPAIR {desc}'

&#x20;           lines = \[desc] + list(op.get('continuations', \[]))

&#x20;           labour\_rows.append({

&#x20;               'guide':      op.get('guide', ''),

&#x20;               'desc\_lines': lines,

&#x20;               'wu':         f"{op\['wu']:.1f}\*",

&#x20;           })

&#x20;

&#x20;       elif t == 'rnr':

&#x20;           lines = \[op\['desc'].upper()] + list(op.get('continuations', \[]))

&#x20;           labour\_rows.append({

&#x20;               'guide':      op.get('guide', ''),

&#x20;               'desc\_lines': lines,

&#x20;               'wu':         f"{op\['wu']:.1f}",

&#x20;           })

&#x20;

&#x20;       elif t == 'check\_labour':

&#x20;           # "CHECK \[panel]" labour line that lands in EVA's Check category

&#x20;           desc = op.get('desc', '').upper()

&#x20;           if not desc.startswith('CHECK'):

&#x20;               desc = f'CHECK {desc}'

&#x20;           lines = \[desc] + list(op.get('continuations', \[]))

&#x20;           labour\_rows.append({

&#x20;               'guide':      op.get('guide', ''),

&#x20;               'desc\_lines': lines,

&#x20;               'wu':         f"{op\['wu']:.1f}\*",

&#x20;           })

&#x20;

&#x20;       elif t == 'paint\_new':

&#x20;           paint\_rows.append({

&#x20;               'guide': op.get('guide', ''),

&#x20;               'desc':  f"{op\['desc'].upper()} NEW PART PAINT K1R",

&#x20;               'wu':    f"{op\['wu']:.1f}",

&#x20;           })

&#x20;

&#x20;       elif t == 'paint\_repair':

&#x20;           paint\_rows.append({

&#x20;               'guide': op.get('guide', ''),

&#x20;               'desc':  f"{op\['desc'].upper()} REPAIR PAINTING <50%",

&#x20;               'wu':    f"{op\['wu']:.1f}",

&#x20;           })

&#x20;

&#x20;       elif t == 'paint\_blend':

&#x20;           paint\_rows.append({

&#x20;               'guide': op.get('guide', ''),

&#x20;               'desc':  f"{op\['desc'].upper()} SURFACE PAINT",

&#x20;               'wu':    f"{op\['wu']:.1f}",

&#x20;           })

&#x20;

&#x20;       elif t == 'paint\_prep':

&#x20;           paint\_rows.append({

&#x20;               'guide': op.get('guide', ''),

&#x20;               'desc':  'PREPARATION FOR PRE-PAINTING',

&#x20;               'wu':    f"{op\['wu']:.1f}",

&#x20;           })

&#x20;

&#x20;       elif t == 'new\_part':

&#x20;           price\_str = f"£{op\['price']:,.2f}"

&#x20;           if op.get('unpriced'):

&#x20;               price\_str += ' \*'

&#x20;           parts\_rows.append({

&#x20;               'guide':    op.get('guide', ''),

&#x20;               'desc':     op\['desc'].upper(),

&#x20;               'part\_num': op.get('part\_num', ''),

&#x20;               'bet':      op.get('bet', '0%'),

&#x20;               'price':    price\_str,

&#x20;           })

&#x20;

&#x20;       elif t == 'specialist\_fixed':

&#x20;           extras\_rows.append({

&#x20;               'desc':  op\['desc'].upper(),

&#x20;               'type':  op.get('text', 'Specialist'),

&#x20;               'bet':   op.get('bet', '0%'),

&#x20;               'price': f"£{op\['price']:,.2f}",

&#x20;           })

&#x20;

&#x20;       elif t == 'specialist\_wu':

&#x20;           price\_val = op\['wu'] / 10.0 \* rate

&#x20;           extras\_rows.append({

&#x20;               'desc':  op\['desc'].upper(),

&#x20;               'type':  op.get('text', 'Specialist'),

&#x20;               'bet':   op.get('bet', '0%'),

&#x20;               'price': f"£{price\_val:,.2f}",

&#x20;           })

&#x20;

&#x20;       else:

&#x20;           raise ValueError(f"Unknown operation type: {t}")

&#x20;

&#x20;   return {

&#x20;       'labour\_rows': labour\_rows,

&#x20;       'paint\_rows':  paint\_rows,

&#x20;       'parts\_rows':  parts\_rows,

&#x20;       'extras\_rows': extras\_rows,

&#x20;   }

&#x20;

&#x20;

\# ─── Section drawers — each knows how to render its own header + rows ───────

def draw\_summary\_and\_vehicle(w, data):

&#x20;   """Summary Information + Vehicle Details + Vehicle Condition (one shot)."""

&#x20;   w.text(20.0, 'Summary Information', FONT\_BOLD, SIZE\_H2)

&#x20;   w.advance(4)

&#x20;   w.rule()

&#x20;   w.advance(13)

&#x20;   w.text(20.0, 'Claim', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(13)

&#x20;

&#x20;   claim\_rows = \[

&#x20;       ('Authorisation Status:', 'Interim',                  'Date of Incident:',           'Not Known'),

&#x20;       ('Work Provider:',        'OTHER',                    'Able to Authorise Repairs:',  'TBA'),

&#x20;       ('Claim Reference:',      data.get('claim\_ref', ''),  'Repairs Authorised?',         'TBA'),

&#x20;       ('Policy Number:',        '',                         'VAT Portion Payable:',        'TBA'),

&#x20;       ('Other Reference:',      '',                         'Repairer:',                   ''),

&#x20;       ('Estimated Repair Time', '',                         '',                             ''),

&#x20;       ('(Working Days):',       '',                         '',                             ''),

&#x20;   ]

&#x20;   for row in claim\_rows:

&#x20;       w.text(CLAIM\_COL1\_X, row\[0])

&#x20;       w.text(CLAIM\_COL2\_X, row\[1])

&#x20;       w.text(CLAIM\_COL3\_X, row\[2])

&#x20;       w.text(CLAIM\_COL4\_X, row\[3])

&#x20;       w.advance(11)

&#x20;

&#x20;   w.advance(6)

&#x20;   w.text(20.0, 'Vehicle Details', FONT\_BOLD, SIZE\_H2)

&#x20;   w.advance(4)

&#x20;   w.rule()

&#x20;   w.advance(13)

&#x20;

&#x20;   w.text(VEH\_LABEL\_X, 'Vehicle',     FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text(VEH\_SPECS\_X, 'Model Specs', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(13)

&#x20;

&#x20;   veh = data\['vehicle']

&#x20;   veh\_rows = \[

&#x20;       ('Manufacturer:',        veh\['manufacturer']),

&#x20;       ('Model:',               veh\['model']),

&#x20;       ('Model Sheet Number:',  veh\['model\_sheet']),

&#x20;       ('Engine:',              veh\['engine']),

&#x20;       ('Registration Number:', veh\['reg']),

&#x20;       ('VIN Number:',          veh\['vin']),

&#x20;       ('Registration Month:',  veh\['reg\_month']),

&#x20;       ('Registration Year:',   veh\['reg\_year']),

&#x20;       ('Odometer:',            veh.get('odometer', 'Not Known')),

&#x20;       ('Colour:',              veh\['colour']),

&#x20;       ('Paint Code:',          veh\['paint\_code']),

&#x20;       ('Build Date:',          veh\['build\_date']),

&#x20;       ('Selection Type:',      veh.get('selection', 'AudaVIN+')),

&#x20;       ('Fuel Type:',           veh\['fuel']),

&#x20;       ('Vehicle Imported:',    veh.get('imported', 'No')),

&#x20;   ]

&#x20;   # Interleave vehicle rows and specs rows — left vs right columns.

&#x20;   # They're the same height so we can just render them row by row,

&#x20;   # using fallback empty strings.

&#x20;   specs = list(veh\['specs'])

&#x20;   n\_rows = max(len(veh\_rows), len(specs))

&#x20;   for i in range(n\_rows):

&#x20;       if i < len(veh\_rows):

&#x20;           w.text(VEH\_LABEL\_X, veh\_rows\[i]\[0])

&#x20;           w.text(VEH\_VAL\_X,   str(veh\_rows\[i]\[1]))

&#x20;       if i < len(specs):

&#x20;           w.text(VEH\_SPECS\_X, specs\[i])

&#x20;       w.advance(11)

&#x20;

&#x20;   w.advance(6)

&#x20;   w.text(20.0, 'Vehicle Condition', FONT\_BOLD, SIZE\_H2)

&#x20;   w.advance(4)

&#x20;   w.rule()

&#x20;   w.advance(13)

&#x20;   w.text(20.0, 'Vehicle Status', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(13)

&#x20;

&#x20;   cond\_rows = \[

&#x20;       ('Pre-Accident Condition:', '', 'Severity of Impact:',           ''),

&#x20;       ('Steering Rim Ply:',       '', 'Vehicle Status on Inspection:', ''),

&#x20;       ('Brakes Pedal Travel:',    '', 'Date of Inspection:',           data.get('inspection\_date', '')),

&#x20;       ('Place of Inspection:',    '', '',                              ''),

&#x20;       ('Pre-Accident Damage:',    '', '',                              ''),

&#x20;   ]

&#x20;   for row in cond\_rows:

&#x20;       w.text(CLAIM\_COL1\_X, row\[0])

&#x20;       w.text(CLAIM\_COL2\_X, row\[1])

&#x20;       w.text(CLAIM\_COL3\_X, row\[2])

&#x20;       w.text(CLAIM\_COL4\_X, row\[3])

&#x20;       w.advance(11)

&#x20;

&#x20;

def draw\_tyres\_and\_damage(w, data):

&#x20;   """Tyres Condition + Damage Areas block."""

&#x20;   w.text(20.0, 'Tyres Condition:', FONT, SIZE\_BODY)

&#x20;   w.advance(11)

&#x20;   w.text(27.0, 'Tread Depth LHF:', FONT, SIZE\_BODY)

&#x20;   w.text(CLAIM\_COL3\_X, 'Tread Depth RHF:', FONT, SIZE\_BODY)

&#x20;   w.advance(11)

&#x20;   w.text(27.0, 'Tread Depth LHR:', FONT, SIZE\_BODY)

&#x20;   w.text(CLAIM\_COL3\_X, 'Tread Depth RHR:', FONT, SIZE\_BODY)

&#x20;   w.advance(15)

&#x20;   w.text(20.0, 'Damage Areas:', FONT, SIZE\_BODY)

&#x20;   w.text(CLAIM\_COL3\_X, 'Direction of Impact:', FONT, SIZE\_BODY)

&#x20;   w.advance(20)

&#x20;

&#x20;

def draw\_cost\_summary(w, data, totals):

&#x20;   """Addresses + Cost Summary (fixed block of rows)."""

&#x20;   w.text(20.0, 'Addresses', FONT\_BOLD, SIZE\_H2)

&#x20;   w.advance(4)

&#x20;   w.rule()

&#x20;   w.advance(22)

&#x20;   w.text(21.0, 'No addresses entered.', FONT, SIZE\_BODY)

&#x20;   w.advance(24)

&#x20;   w.text(20.0, 'Cost Summary', FONT\_BOLD, SIZE\_H2)

&#x20;   w.advance(18)

&#x20;

&#x20;   cost\_rows = \[

&#x20;       ('Total Labour',         f"£{totals\['total\_labour']:,.2f}"),

&#x20;       ('Total Paint/Material', f"£{totals\['total\_paint\_material']:,.2f}"),

&#x20;       ('Total Parts',          f"£{totals\['total\_parts']:,.2f}"),

&#x20;       ('Additional Costs',     f"£{totals\['total\_extras']:,.2f}"),

&#x20;       ('Grand Total Exc VAT:', f"£{totals\['grand\_ex\_vat']:,.2f}"),

&#x20;       ('20 % VAT:',            f"£{totals\['vat']:,.2f}"),

&#x20;       ('Grand Total Inc VAT:', f"£{totals\['grand\_inc\_vat']:,.2f}"),

&#x20;       ('Excess:',              'TBA'),

&#x20;   ]

&#x20;   for label, val in cost\_rows:

&#x20;       w.text(COST\_LABEL\_X, label, FONT, SIZE\_BODY)

&#x20;       w.text\_right(COST\_VAL\_RIGHT, val, FONT, SIZE\_BODY)

&#x20;       w.advance(ROW\_H\_SUMMARY)

&#x20;

&#x20;

def labour\_header(w, rate):

&#x20;   """Draw the LABOUR section header + column headers. Reused on continuation pages."""

&#x20;   w.advance(12)

&#x20;   w.text(20.0, 'Repair Information', FONT\_BOLD, SIZE\_H2)

&#x20;   w.advance(4)

&#x20;   w.rule()

&#x20;   w.advance(20)

&#x20;   w.text(20.0, 'LABOUR', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text(291.3, f'Time Basis 10 WU = 1 HR. Price = £{rate:.2f}/HR', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(23)

&#x20;   w.text(LAB\_GUIDE\_X, 'Repair / Guide', FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.advance(14)

&#x20;   w.text(LAB\_GUIDE\_X, 'Number',         FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text(LAB\_DESC\_X,  'Description',    FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text\_right(LAB\_WU\_RIGHT\_X, 'Work Units', FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.advance(22)

&#x20;

&#x20;

def labour\_continuation\_header(w):

&#x20;   """Real Audatex does not redraw any headers on continuation pages —

&#x20;   it just continues the rows. EVA's parser expects this; adding a

&#x20;   'LABOUR (continued)' header causes ghost rows to appear in EVA.

&#x20;   So we intentionally do nothing here."""

&#x20;   pass

&#x20;

&#x20;

def draw\_labour(w, compiled, totals, rate):

&#x20;   """Labour table, handles arbitrary row count with page breaks."""

&#x20;   labour\_header(w, rate)

&#x20;   w.on\_new\_page = labour\_continuation\_header

&#x20;

&#x20;   for item in compiled\['labour\_rows']:

&#x20;       n\_lines = len(item\['desc\_lines'])

&#x20;       needed = n\_lines \* ROW\_H\_TABLE + 2

&#x20;       w.ensure\_space(needed)

&#x20;       start\_y = w.y

&#x20;       w.text(LAB\_GUIDE\_X, item\['guide'], FONT, SIZE\_TABLE)

&#x20;       for i, line in enumerate(item\['desc\_lines']):

&#x20;           w.text(LAB\_DESC\_X, line, FONT, SIZE\_TABLE)

&#x20;           if i < n\_lines - 1:

&#x20;               w.advance(ROW\_H\_TABLE)

&#x20;       if item.get('wu'):

&#x20;           # WU goes on the first row of the item

&#x20;           # Save y, move back to first line, draw, restore

&#x20;           saved\_y = w.y

&#x20;           w.y = start\_y

&#x20;           w.text\_right(LAB\_WU\_RIGHT\_X, item\['wu'], FONT, SIZE\_TABLE)

&#x20;           w.y = saved\_y

&#x20;       w.advance(ROW\_H\_TABLE)

&#x20;

&#x20;   w.on\_new\_page = None

&#x20;

&#x20;   # Totals — ensure they land on the same page as the last row if possible,

&#x20;   # but since they're bold and small, let them break if needed.

&#x20;   w.ensure\_space(30)

&#x20;   w.advance(12)

&#x20;   w.text(258.8, 'Total Work Units', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(LAB\_WU\_RIGHT\_X, f"{totals\['labour\_wu']:.1f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(12)

&#x20;   w.text(171.7, 'Total Panel / Mechanical Labour', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text(412.3, f"{totals\['labour\_hours']:.2f} HRS", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(547.1, f"£{totals\['labour\_cost']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(18)

&#x20;   w.text\_center(PAGE\_W/2, '\*NOTE TIME BASIS = 10 WU / HOUR\*', FONT, SIZE\_TABLE)

&#x20;   w.advance(11)

&#x20;   w.text\_center(PAGE\_W/2,

&#x20;                 '\*OPINION TIMES ENTERED HAVE BEEN CONVERTED TO MATCH MANUFACTURERS TIMES\*',

&#x20;                 FONT, SIZE\_TABLE)

&#x20;   w.advance(20)

&#x20;

&#x20;

def paint\_header(w, rate, coat\_type):

&#x20;   w.text(20.0, 'PAINT WORK', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text(291.3, f'Time Basis 10 WU = 1 HR. Price = £{rate:.2f}/HR', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(23)

&#x20;   w.text(LAB\_GUIDE\_X, 'Repair /Guide',  FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text(LAB\_DESC\_X,  'Description',    FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.advance(14)

&#x20;   w.text(LAB\_GUIDE\_X, 'Number',         FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text(LAB\_DESC\_X,  coat\_type,        FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text\_right(LAB\_WU\_RIGHT\_X, 'Work Units', FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.advance(22)

&#x20;

&#x20;

def paint\_continuation\_header(w):

&#x20;   """No continuation header — real Audatex just continues rows."""

&#x20;   pass

&#x20;

&#x20;

def draw\_paint(w, compiled, totals, rate, coat\_type):

&#x20;   w.ensure\_space(80)

&#x20;   paint\_header(w, rate, coat\_type)

&#x20;   w.on\_new\_page = paint\_continuation\_header

&#x20;

&#x20;   for item in compiled\['paint\_rows']:

&#x20;       w.ensure\_space(ROW\_H\_TABLE + 2)

&#x20;       w.text(LAB\_GUIDE\_X, item.get('guide', ''), FONT, SIZE\_TABLE)

&#x20;       w.text(LAB\_DESC\_X,  item.get('desc', ''),  FONT, SIZE\_TABLE)

&#x20;       if item.get('wu'):

&#x20;           w.text\_right(LAB\_WU\_RIGHT\_X, item\['wu'], FONT, SIZE\_TABLE)

&#x20;       w.advance(ROW\_H\_TABLE)

&#x20;

&#x20;   w.on\_new\_page = None

&#x20;

&#x20;   # Paint totals

&#x20;   w.ensure\_space(30)

&#x20;   w.advance(12)

&#x20;   w.text(258.8, 'Total Work Units', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(LAB\_WU\_RIGHT\_X, f"{totals\['paint\_wu']:.1f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(12)

&#x20;   w.text(221.4, 'Total Paintwork Labour', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text(415.0, f"{totals\['paint\_hours']:.1f} HRS.", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(547.1, f"£{totals\['paint\_cost']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(22)

&#x20;

&#x20;

def draw\_paint\_materials(w, totals):

&#x20;   w.ensure\_space(100)

&#x20;   w.text(20.0, 'MATERIAL COST - PAINT', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(547.1, 'COST', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(16)

&#x20;

&#x20;   mat\_rows = \[

&#x20;       ('Total Paint Cost',                    f"£{totals\['paint\_material\_base']:,.2f}"),

&#x20;       ('Sundry Paint Material',               f"£{totals\['sundry\_paint']:,.2f}"),

&#x20;       ('Pre-Painting Sundry Materials',       f"£{totals\['pre\_sundry']:,.2f}"),

&#x20;       ('Total Excluding Pearlescent Uplift',  f"£{totals\['total\_paint\_material']:,.2f}"),

&#x20;       ('Pearlescent Uplift @ 0.0%',           '£0.00'),

&#x20;       ('Total Paint And Material Cost',       f"£{totals\['total\_paint\_material']:,.2f}"),

&#x20;   ]

&#x20;   for i, (label, val) in enumerate(mat\_rows):

&#x20;       w.text(103.25, label, FONT, SIZE\_BODY)

&#x20;       w.text\_right(547.1, val, FONT, SIZE\_BODY)

&#x20;       w.advance(11)

&#x20;       if i == 2:

&#x20;           w.advance(5)

&#x20;       if i == 4:

&#x20;           w.advance(5)

&#x20;   w.advance(12)

&#x20;

&#x20;

def parts\_header(w, price\_valid):

&#x20;   w.text(20.0, 'PARTS', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(12)

&#x20;   w.text\_right(547.1, f"Price Valid: {price\_valid}", FONT, SIZE\_BODY)

&#x20;   w.advance(22)

&#x20;   w.text(PRT\_GUIDE\_X,   'Guide No.',   FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text(PRT\_DESC\_X,    'Description', FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text(PRT\_PARTNUM\_X, 'Part Number', FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text(PRT\_BET\_X,     'Bet.',        FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text\_right(PRT\_PRICE\_RIGHT, 'Price', FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.advance(ROW\_H\_TABLE + 10)

&#x20;

&#x20;

def parts\_continuation\_header(w):

&#x20;   """No continuation header — real Audatex just continues rows."""

&#x20;   pass

&#x20;

&#x20;

def draw\_parts(w, compiled, totals, sundry\_pct, price\_valid):

&#x20;   w.ensure\_space(60)

&#x20;   parts\_header(w, price\_valid)

&#x20;   w.on\_new\_page = parts\_continuation\_header

&#x20;

&#x20;   # Parts Description column: from x=103.25 to x=242.00 = \~138pt wide

&#x20;   MAX\_PRT\_DESC\_WIDTH = PRT\_PARTNUM\_X - PRT\_DESC\_X - 4

&#x20;   # Parts Part Number column: from x=242 to x=353 = \~111pt wide

&#x20;   MAX\_PRT\_NUM\_WIDTH = PRT\_BET\_X - PRT\_PARTNUM\_X - 4

&#x20;

&#x20;   for item in compiled\['parts\_rows']:

&#x20;       w.ensure\_space(ROW\_H\_TABLE + 2)

&#x20;       desc = item.get('desc', '')

&#x20;       if stringWidth(desc, FONT, SIZE\_TABLE) > MAX\_PRT\_DESC\_WIDTH:

&#x20;           while desc and stringWidth(desc + '...', FONT, SIZE\_TABLE) > MAX\_PRT\_DESC\_WIDTH:

&#x20;               desc = desc\[:-1]

&#x20;           desc = desc.rstrip() + '...'

&#x20;       part\_num = item.get('part\_num', '')

&#x20;       if stringWidth(part\_num, FONT, SIZE\_TABLE) > MAX\_PRT\_NUM\_WIDTH:

&#x20;           while part\_num and stringWidth(part\_num + '...', FONT, SIZE\_TABLE) > MAX\_PRT\_NUM\_WIDTH:

&#x20;               part\_num = part\_num\[:-1]

&#x20;           part\_num = part\_num.rstrip() + '...'

&#x20;       w.text(PRT\_GUIDE\_X,   item.get('guide', ''),   FONT, SIZE\_TABLE)

&#x20;       w.text(PRT\_DESC\_X,    desc,                    FONT, SIZE\_TABLE)

&#x20;       w.text(PRT\_PARTNUM\_X, part\_num,                FONT, SIZE\_TABLE)

&#x20;       w.text(PRT\_BET\_X,     item.get('bet', '0%'),   FONT, SIZE\_TABLE)

&#x20;       w.text\_right(PRT\_PRICE\_RIGHT, item.get('price', ''), FONT, SIZE\_TABLE)

&#x20;       w.advance(ROW\_H\_TABLE)

&#x20;

&#x20;   w.on\_new\_page = None

&#x20;

&#x20;   # Totals

&#x20;   w.ensure\_space(60)

&#x20;   if compiled\['parts\_rows']:

&#x20;       w.advance(12)

&#x20;       w.text(257.5, 'Sub Total', FONT, SIZE\_BODY)

&#x20;       w.text\_right(547.1, f"£{totals\['parts\_subtotal']:,.2f}", FONT, SIZE\_BODY)

&#x20;       w.advance(11)

&#x20;       w.text(257.5, 'Deduction from RRP', FONT, SIZE\_BODY)

&#x20;       w.text(412.0, '(0.0 %)', FONT, SIZE\_BODY)

&#x20;       w.text\_right(547.1, '£0.00', FONT, SIZE\_BODY)

&#x20;       w.advance(11)

&#x20;       w.text(257.5, 'Sundry Parts', FONT, SIZE\_BODY)

&#x20;       w.text(412.0, f"({sundry\_pct} %)", FONT, SIZE\_BODY)

&#x20;       w.text\_right(547.1, f"£{totals\['parts\_sundry']:,.2f}", FONT, SIZE\_BODY)

&#x20;       w.advance(11)

&#x20;       w.text(384.0, 'Total Parts', FONT\_BOLD, SIZE\_BODY)

&#x20;       w.text\_right(547.1, f"£{totals\['total\_parts']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   else:

&#x20;       w.advance(12)

&#x20;       w.text(257.5, 'Sub Total', FONT, SIZE\_BODY)

&#x20;       w.text\_right(547.1, '£0.00', FONT, SIZE\_BODY)

&#x20;       w.advance(11)

&#x20;       w.text(384.0, 'Total Parts', FONT\_BOLD, SIZE\_BODY)

&#x20;       w.text\_right(547.1, '£0.00', FONT\_BOLD, SIZE\_BODY)

&#x20;

&#x20;   w.advance(18)

&#x20;   w.text\_center(PAGE\_W/2, 'NB - COLOUR CODED ITEMS/TRIM - PART NUMBERS MAY DIFFER',

&#x20;                 FONT, SIZE\_TABLE)

&#x20;   w.advance(22)

&#x20;

&#x20;

def extras\_header(w):

&#x20;   w.text(20.0, 'Extras', FONT\_BOLD, SIZE\_H2)

&#x20;   w.advance(14)

&#x20;   w.text(EXT\_DESC\_X, 'Description', FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text(325.2,      'Betterment',  FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.text\_right(EXT\_PRICE\_RIGHT, 'Price', FONT\_BOLD, SIZE\_TABLE\_HDR)

&#x20;   w.advance(ROW\_H\_TABLE)

&#x20;

&#x20;

def extras\_continuation\_header(w):

&#x20;   """No continuation header — real Audatex just continues rows."""

&#x20;   pass

&#x20;

&#x20;

def draw\_extras(w, compiled, totals):

&#x20;   w.ensure\_space(40)

&#x20;   extras\_header(w)

&#x20;   w.on\_new\_page = extras\_continuation\_header

&#x20;

&#x20;   # Max width for description column in extras: from EXT\_DESC\_X (103.25) to EXT\_SPEC\_X (214.25)

&#x20;   # = 111pt. At 7.5pt Helvetica, that's roughly 20-22 chars — leave a small gap.

&#x20;   # We truncate with an ellipsis if too long.

&#x20;   MAX\_DESC\_WIDTH = EXT\_SPEC\_X - EXT\_DESC\_X - 4  # leave 4pt gap before Specialist

&#x20;

&#x20;   for item in compiled\['extras\_rows']:

&#x20;       w.ensure\_space(ROW\_H\_TABLE + 2)

&#x20;       desc = item\['desc']

&#x20;       # Truncate if too wide

&#x20;       desc\_w = stringWidth(desc, FONT, SIZE\_TABLE)

&#x20;       if desc\_w > MAX\_DESC\_WIDTH:

&#x20;           # Binary-ish truncation with ellipsis

&#x20;           while desc and stringWidth(desc + '...', FONT, SIZE\_TABLE) > MAX\_DESC\_WIDTH:

&#x20;               desc = desc\[:-1]

&#x20;           desc = desc.rstrip() + '...'

&#x20;       w.text(EXT\_DESC\_X, desc, FONT, SIZE\_TABLE)

&#x20;       w.text(EXT\_SPEC\_X, item.get('type', 'Specialist'), FONT, SIZE\_TABLE)

&#x20;       w.text(EXT\_BET\_X,  item.get('bet', '0%'), FONT, SIZE\_TABLE)

&#x20;       w.text\_right(EXT\_PRICE\_RIGHT, item\['price'], FONT, SIZE\_TABLE)

&#x20;       w.advance(ROW\_H\_TABLE)

&#x20;

&#x20;   w.on\_new\_page = None

&#x20;

&#x20;   w.ensure\_space(20)

&#x20;   w.advance(5)

&#x20;   w.text(EXT\_BET\_X, 'Total Extras', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(EXT\_PRICE\_RIGHT, f"£{totals\['total\_extras']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(22)

&#x20;

&#x20;

def draw\_calculation(w, data, totals):

&#x20;   w.ensure\_space(300)

&#x20;   w.text(20.0, 'Calculation', FONT\_BOLD, SIZE\_H2)

&#x20;   w.text\_right(PAGE\_W - 20.0, data.get('calc\_date', ''), FONT\_BOLD, SIZE\_H2)

&#x20;   w.advance(4)

&#x20;   w.rule()

&#x20;   w.advance(16)

&#x20;

&#x20;   w.text(20.0, 'Labour', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(12)

&#x20;   w.text(158.75, 'Total Panel/Mechanical', FONT, SIZE\_BODY)

&#x20;   w.text\_right(440.0, f"£{totals\['labour\_cost']:,.2f}", FONT, SIZE\_BODY)

&#x20;   w.advance(11)

&#x20;   w.text(158.75, 'Total Paintwork', FONT, SIZE\_BODY)

&#x20;   w.text\_right(440.0, f"£{totals\['paint\_cost']:,.2f}", FONT, SIZE\_BODY)

&#x20;   w.advance(11)

&#x20;   w.text(20.0, 'Total Labour', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(PAGE\_W - 20.0, f"£{totals\['total\_labour']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(18)

&#x20;

&#x20;   w.text(20.0, 'Total Paint/Material', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(PAGE\_W - 20.0, f"£{totals\['total\_paint\_material']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(11)

&#x20;   w.text(20.0, 'Costs', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(11)

&#x20;   w.text(20.0, 'Total Parts', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(PAGE\_W - 20.0, f"£{totals\['total\_parts']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(18)

&#x20;

&#x20;   w.text(20.0, 'Additional Costs', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(12)

&#x20;   w.text(158.75, 'Cost of Specialist', FONT, SIZE\_BODY)

&#x20;   w.text\_right(440.0, f"£{totals\['total\_extras']:,.2f}", FONT, SIZE\_BODY)

&#x20;   w.advance(11)

&#x20;   w.text(20.0, 'Total Additional Costs', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(PAGE\_W - 20.0, f"£{totals\['total\_extras']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(18)

&#x20;

&#x20;   w.text(20.0, 'Grand Total Excl VAT', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(PAGE\_W - 20.0, f"£{totals\['grand\_ex\_vat']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(12)

&#x20;   w.text(158.75, 'VAT @ 20 %', FONT, SIZE\_BODY)

&#x20;   w.text\_right(440.0, f"£{totals\['vat']:,.2f}", FONT, SIZE\_BODY)

&#x20;   w.advance(11)

&#x20;   w.text(20.0, 'Grand Total Incl VAT', FONT\_BOLD, SIZE\_BODY)

&#x20;   w.text\_right(PAGE\_W - 20.0, f"£{totals\['grand\_inc\_vat']:,.2f}", FONT\_BOLD, SIZE\_BODY)

&#x20;   w.advance(18)

&#x20;   w.text(158.75, 'Excess', FONT, SIZE\_BODY)

&#x20;   w.text\_right(440.0, 'TBA', FONT, SIZE\_BODY)

&#x20;   w.advance(22)

&#x20;

&#x20;   w.text(20.0, 'Assessment Notes', FONT\_BOLD, SIZE\_H2)

&#x20;   w.advance(12)

&#x20;

&#x20;   notes = data.get('notes', 'No assessment notes entered.')

&#x20;   max\_chars = 100

&#x20;   words = notes.split()

&#x20;   line = ''

&#x20;   for word in words:

&#x20;       if len(line) + len(word) + 1 > max\_chars:

&#x20;           w.text(20.0, line, FONT, SIZE\_BODY)

&#x20;           w.advance(11)

&#x20;           line = word

&#x20;       else:

&#x20;           line = f'{line} {word}' if line else word

&#x20;   if line:

&#x20;       w.text(20.0, line, FONT, SIZE\_BODY)

&#x20;

&#x20;

\# ─── Totals calculator (from `Later`/`unallocated`) ─────────────────────────────────────────────

def compute\_totals(compiled, data):

&#x20;   rate = data\['rates']\['labour\_rate']

&#x20;   paint\_rate = data\['rates']\['paint\_rate']

&#x20;   sundry\_pct = data\['rates']\['sundry\_parts\_pct']

&#x20;   sundry\_paint = data\['rates']\['sundry\_paint']

&#x20;   pre\_sundry = data\['rates']\['pre\_sundry']

&#x20;   paint\_material\_base = data\['rates'].get('paint\_material\_base', 0.0)

&#x20;

&#x20;   labour\_wu = 0.0

&#x20;   for r in compiled\['labour\_rows']:

&#x20;       try:

&#x20;           labour\_wu += float(r\['wu'].rstrip('\*'))

&#x20;       except (KeyError, ValueError, AttributeError):

&#x20;           pass

&#x20;   labour\_hours = labour\_wu / 10.0

&#x20;   labour\_cost  = labour\_hours \* rate

&#x20;

&#x20;   paint\_wu = 0.0

&#x20;   for r in compiled\['paint\_rows']:

&#x20;       try:

&#x20;           paint\_wu += float(r\['wu'].rstrip('\*'))

&#x20;       except (KeyError, ValueError, AttributeError):

&#x20;           pass

&#x20;   paint\_hours = paint\_wu / 10.0

&#x20;   paint\_cost  = paint\_hours \* paint\_rate

&#x20;

&#x20;   total\_labour = labour\_cost + paint\_cost

&#x20;   total\_paint\_material = paint\_material\_base + sundry\_paint + pre\_sundry

&#x20;

&#x20;   parts\_subtotal = 0.0

&#x20;   for r in compiled\['parts\_rows']:

&#x20;       try:

&#x20;           # Strip asterisk suffix if present, e.g. '£25.00 \*'

&#x20;           price\_str = r\['price'].replace('£', '').replace(',', '').rstrip('\*').strip()

&#x20;           parts\_subtotal += float(price\_str)

&#x20;       except (KeyError, ValueError):

&#x20;           pass

&#x20;   parts\_sundry = parts\_subtotal \* sundry\_pct / 100.0 if parts\_subtotal > 0 else 0.0

&#x20;   total\_parts  = parts\_subtotal + parts\_sundry

&#x20;

&#x20;   total\_extras = 0.0

&#x20;   for r in compiled\['extras\_rows']:

&#x20;       try:

&#x20;           total\_extras += float(r\['price'].replace('£', '').replace(',', ''))

&#x20;       except (KeyError, ValueError):

&#x20;           pass

&#x20;

&#x20;   grand\_ex\_vat = total\_labour + total\_paint\_material + total\_parts + total\_extras

&#x20;   vat = grand\_ex\_vat \* 0.20

&#x20;   grand\_inc\_vat = grand\_ex\_vat + vat

&#x20;

&#x20;   return {

&#x20;       'labour\_wu':              labour\_wu,

&#x20;       'labour\_hours':           labour\_hours,

&#x20;       'labour\_cost':            labour\_cost,

&#x20;       'paint\_wu':               paint\_wu,

&#x20;       'paint\_hours':            paint\_hours,

&#x20;       'paint\_cost':             paint\_cost,

&#x20;       'total\_labour':           total\_labour,

&#x20;       'paint\_material\_base':    paint\_material\_base,

&#x20;       'sundry\_paint':           sundry\_paint,

&#x20;       'pre\_sundry':             pre\_sundry,

&#x20;       'total\_paint\_material':   total\_paint\_material,

&#x20;       'parts\_subtotal':         parts\_subtotal,

&#x20;       'parts\_sundry':           parts\_sundry,

&#x20;       'total\_parts':            total\_parts,

&#x20;       'total\_extras':           total\_extras,

&#x20;       'grand\_ex\_vat':           grand\_ex\_vat,

&#x20;       'vat':                    vat,

&#x20;       'grand\_inc\_vat':          grand\_inc\_vat,

&#x20;   }

&#x20;

&#x20;

\# ─── Build the PDF — two-pass approach for accurate page count ───────────────

def build\_pdf(output\_path, data):

&#x20;   compiled = compile\_assessment(data)

&#x20;   totals = compute\_totals(compiled, data)

&#x20;

&#x20;   def render(output\_target, placeholder\_total='?'):

&#x20;       """Render the full document. Returns the page count actually produced."""

&#x20;       c = canvas.Canvas(output\_target, pagesize=A4)

&#x20;       c.setTitle(f'Audatex Estimate — {data\["assessment\_number"]}')

&#x20;       c.setAuthor('Collision Engineers')

&#x20;

&#x20;       w = PageWriter(c, data\['assessment\_number'], data\['version'], data\['printed'])

&#x20;

&#x20;       # Pages flow naturally — PageWriter breaks when content doesn't fit.

&#x20;       draw\_summary\_and\_vehicle(w, data)

&#x20;       draw\_tyres\_and\_damage(w, data)

&#x20;       draw\_cost\_summary(w, data, totals)

&#x20;       draw\_labour(w, compiled, totals, data\['rates']\['labour\_rate'])

&#x20;       draw\_paint(w, compiled, totals, data\['rates']\['paint\_rate'],

&#x20;                  data.get('coat\_type', 'BASECOAT CLEAR'))

&#x20;       draw\_paint\_materials(w, totals)

&#x20;       draw\_parts(w, compiled, totals, data\['rates']\['sundry\_parts\_pct'],

&#x20;                  data.get('price\_valid', ''))

&#x20;       draw\_extras(w, compiled, totals)

&#x20;       draw\_calculation(w, data, totals)

&#x20;

&#x20;       page\_count = w.get\_page\_count()

&#x20;       c.save()

&#x20;       return page\_count

&#x20;

&#x20;   # Pass 1: render to a throwaway buffer to count pages

&#x20;   buf = io.BytesIO()

&#x20;   total\_pages = render(buf)

&#x20;

&#x20;   # Pass 2: we need to substitute the real page count into the page chrome.

&#x20;   # Simplest approach: rewrite the build so it accepts the page count upfront.

&#x20;   # We do this by monkey-patching PageWriter.\_draw\_chrome to use the real total.

&#x20;   # Re-render to actual output file.

&#x20;   original\_draw\_chrome = PageWriter.\_draw\_chrome

&#x20;   def patched\_draw\_chrome(self):

&#x20;       c = self.c

&#x20;       c.setFont(FONT\_BOLD, SIZE\_HEADER)

&#x20;       c.drawCentredString(PAGE\_W/2, PAGE\_H - 32.0, 'TEL: COLLISION ENGINEERS')

&#x20;       c.drawCentredString(PAGE\_W/2, PAGE\_H - 46.6, '01515590762 77-79 HOYLAKE ROAD')

&#x20;       c.drawCentredString(PAGE\_W/2, PAGE\_H - 61.2, 'MORETON')

&#x20;       c.drawCentredString(PAGE\_W/2, PAGE\_H - 75.8, 'WIRRAL, CH46 9PY')

&#x20;       c.setFont(FONT\_BOLD, SIZE\_SUBHDR)

&#x20;       c.drawString(20.0,               PAGE\_H - 90.4, f'Assessment Number: {self.assessment\_number}')

&#x20;       c.drawRightString(PAGE\_W - 20.0, PAGE\_H - 90.4, 'Full Report')

&#x20;       c.drawString(20.0,               PAGE\_H - 105.0, f'Version: {self.version}')

&#x20;       c.drawRightString(PAGE\_W - 20.0, PAGE\_H - 105.0, f'Printed: {self.printed}')

&#x20;       c.setFont(FONT\_BOLD, SIZE\_FOOTER)

&#x20;       c.drawString(20.0, PAGE\_H - 812.0, 'Audatex System Using Manufacturer Times')

&#x20;       c.drawRightString(PAGE\_W - 20.0, PAGE\_H - 818.4,

&#x20;                         f'PAGE {self.page\_num} OF {total\_pages}')

&#x20;

&#x20;   PageWriter.\_draw\_chrome = patched\_draw\_chrome

&#x20;   try:

&#x20;       render(output\_path)

&#x20;   finally:

&#x20;       PageWriter.\_draw\_chrome = original\_draw\_chrome

&#x20;

&#x20;   print(f'PDF generated: {output\_path}  ({total\_pages} pages)')

&#x20;   return {'compiled': compiled, 'totals': totals, 'total\_pages': total\_pages}

&#x20;

&#x20;

\# ─── Test: reconstruct the 82-line Vauxhall Vivaro test job ──────────────────

if \_\_name\_\_ == '\_\_main\_\_':

&#x20;   data = {

&#x20;       'assessment\_number': 'AI000004',

&#x20;       'version':           'AI/VX15VZH/4',

&#x20;       'printed':           '23/04/2026',

&#x20;       'calc\_date':         '23/04/2026',

&#x20;       'price\_valid':       '23/04/2026',

&#x20;       'claim\_ref':         'TEST',

&#x20;       'inspection\_date':   '23/04/2026',

&#x20;       'coat\_type':         'TWO COAT METALLIC',

&#x20;

&#x20;       'rates': {

&#x20;           'labour\_rate':         80.00,

&#x20;           'paint\_rate':          80.00,

&#x20;           'sundry\_parts\_pct':    3.5,

&#x20;           'sundry\_paint':        120.16,

&#x20;           'pre\_sundry':           0.00,

&#x20;           'paint\_material\_base': 4408.50,

&#x20;       },

&#x20;

&#x20;       'vehicle': {

&#x20;           'manufacturer': 'VAUXHALL',

&#x20;           'model':        'VIVARO Base Model',

&#x20;           'model\_sheet':  '592',

&#x20;           'engine':       '1.6 LTR 84/5/8/9 KW',

&#x20;           'reg':          'VX15VZH',

&#x20;           'vin':          'W0L3F7018FV619528',

&#x20;           'reg\_month':    'March',

&#x20;           'reg\_year':     '2015',

&#x20;           'colour':       'GREY',

&#x20;           'paint\_code':   '10H',

&#x20;           'build\_date':   'FROM 02/2015',

&#x20;           'fuel':         'Diesel',

&#x20;           'specs': \[

&#x20;               'FROM 02/2015', 'AIR CONDITIONING', 'ELIMINATE INN MIRROR',

&#x20;               'C-LOCKING W/DEADLOCK', 'F-REGULATOR COMFORT',

&#x20;               'ELECT/HEAT D/MIRROR', 'RADIO CD 18 BT',

&#x20;               'DIGITAL RADIOSYSTEM', 'FOG LAMPS',

&#x20;               'DAYTIME RUN LIGH LED', 'MULTIFUNC S/CUSHION',

&#x20;               'SEAT CLOTH CONNECT', 'IN TRIM SATIN CHROME',

&#x20;               'PARTITION PANEL', 'CHROME MOULDING',

&#x20;               'PARK PILOT SYSTEM', '1.6 LTR 84/5/8/9 KW',

&#x20;               'EMISSION STD EURO 5', 'GEARBOX 6 SPEED',

&#x20;               'LEATHER STRG WHEEL', 'CRUISE CONTROL',

&#x20;               'TYRES 205/65 R16C', 'WHEELS 6J X 16',

&#x20;               'FULL WHEEL COVERS', 'SPARE WHEEL STEEL',

&#x20;               'GWR 2900 KG', 'L/SLIDING DOOR', 'REAR WING DOORS',

&#x20;               'FLAT ROOF', 'WHEELBASE 3498 MM', 'FACTORY LUTON',

&#x20;               'VAN', 'TWO COAT METALLIC',

&#x20;           ],

&#x20;       },

&#x20;

&#x20;       # ─── Operations — mirroring the 82-line Audatex test job ───

&#x20;       'operations': \[

&#x20;

&#x20;           # === LABOUR / R+R operations ===

&#x20;           {'type': 'rnr',   'guide': 'NO NUMBER', 'wu': 2.0,

&#x20;            'desc': 'ALIGN BODY BY HANDHELD MEASURING SYSTEM'},

&#x20;           {'type': 'rnr',   'guide': '0110702',   'wu': 7.0, 'desc': 'R + R FRONT BUMPER'},

&#x20;           {'type': 'rnr',   'guide': '1420120019','wu': 1.0, 'desc': 'R + R FRONT BUMPER IMPACT DAMPER'},

&#x20;           {'type': 'rnr',   'guide': '2040510',   'wu': 2.0, 'desc': 'R + R LEFT HEADLAMP'},

&#x20;           {'type': 'rnr',   'guide': '2040510',   'wu': 2.0, 'desc': 'R + R RIGHT HEADLAMP'},

&#x20;           {'type': 'rnr',   'guide': '2040512',   'wu': 1.0, 'desc': 'ADJUST HEADLAMPS'},

&#x20;           {'type': 'rnr',   'guide': '1410050',   'wu': 13.0, 'desc': 'RENEW BONNET',

&#x20;            'continuations': \['INCLUDES: R + R ENGINE BONNET', 'STRIP + REFIT']},

&#x20;           {'type': 'rnr',   'guide': '2020380',   'wu': 18.0, 'desc': 'R + R WINDSCREEN',

&#x20;            'continuations': \['INCLUDES: R + R COWL TRIM, WIPER ARMS', 'AND A-PILLAR TRIMS']},

&#x20;           {'type': 'rnr',   'guide': '0111540',   'wu': 157.0, 'desc': 'RENEW REAR ROOF'},

&#x20;           {'type': 'rnr',   'guide': '0111540032','wu': 21.0, 'desc': 'RENEW REAR ROOF FRAME'},

&#x20;           {'type': 'rnr',   'guide': '1042980',   'wu': 5.0, 'desc': 'R + R LEFT FRONT DOOR TRIM'},

&#x20;           {'type': 'rnr',   'guide': '0111012',   'wu': 9.0, 'desc': 'R + R LEFT SLIDING DOOR'},

&#x20;           {'type': 'rnr',   'guide': '1412510',   'wu': 18.0, 'desc': 'RENEW L/SLIDING DOOR (REMOVED)'},

&#x20;           {'type': 'rnr',   'guide': '2041490',   'wu': 2.0, 'desc': 'R + R LEFT TAIL LAMP'},

&#x20;           {'type': 'rnr',   'guide': '2041490',   'wu': 2.0, 'desc': 'R + R RIGHT TAIL LAMP'},

&#x20;           {'type': 'rnr',   'guide': '0110732',   'wu': 7.0, 'desc': 'R + R REAR BUMPER CPL'},

&#x20;           {'type': 'rnr',   'guide': 'NO NUMBER', 'wu': 1.0, 'desc': 'R + R REAR REFLECTOR'},

&#x20;           {'type': 'rnr',   'guide': '1016210',   'wu': 1.0, 'desc': 'R + R L/R ROOF PILLAR'},

&#x20;           {'type': 'rnr',   'guide': '0100910',   'wu': 159.0, 'desc': 'RENEW R/R SIDE PANEL',

&#x20;            'continuations': \['INCLUDES: TRIMS AND ATTACHED PARTS REMOVE AND REFIT']},

&#x20;           {'type': 'rnr',   'guide': '1044260',   'wu': 2.0, 'desc': 'R + R LEFT LOADING DOOR TRIM'},

&#x20;           {'type': 'rnr',   'guide': '1415730',   'wu': 1.0, 'desc': 'R + R LEFT REAR DOOR CHECK'},

&#x20;           {'type': 'rnr',   'guide': '1041280',   'wu': 3.0, 'desc': 'R + R L/UPPER B-PILLAR TRIM'},

&#x20;           {'type': 'rnr',   'guide': '1041280',   'wu': 3.0, 'desc': 'R + R R/UPPER B-PILLAR TRIM'},

&#x20;           {'type': 'rnr',   'guide': '1020110',   'wu': 2.0, 'desc': 'R + R FRONT HEADLINING'},

&#x20;           {'type': 'rnr',   'guide': '1415840',   'wu': 9.0, 'desc': 'R + R LOWER LOAD COMPARTMENT PARTITION'},

&#x20;           {'type': 'rnr',   'guide': '0801752',   'wu': 5.0, 'desc': 'R + R L/F SEAT CUSHION'},

&#x20;           {'type': 'rnr',   'guide': '0801742',   'wu': 6.0, 'desc': 'R + R RIGHT FRONT SEAT'},

&#x20;           {'type': 'rnr',   'guide': '6420220',   'wu': 5.0, 'desc': 'R + R PARKING HELP CONTROL UNIT'},

&#x20;           {'type': 'rnr',   'guide': '0801440',   'wu': 1.0, 'desc': 'R + R RIGHT REAR WHEEL'},

&#x20;           {'type': 'rnr',   'guide': '0801440',   'wu': 1.0, 'desc': 'R + R WHEEL/S (ADD/WORK)'},

&#x20;

&#x20;           # === REPAIRS ===

&#x20;           {'type': 'rnr',   'guide': '1509',     'wu': 1.0, 'desc': 'REMOVE\&REFIT L/F DOOR SEAL'},

&#x20;           {'type': 'repair','guide': '1481',     'wu': 15.0, 'desc': 'LEFT FRONT DOOR'},

&#x20;           {'type': 'repair','guide': '2085',     'wu': 50.0, 'desc': 'L/F DOOR FRAME'},

&#x20;           {'type': 'repair','guide': '3744',     'wu': 5.0,  'desc': 'R/R UPPER SIDE PANEL'},

&#x20;

&#x20;           # === SPECIALIST labour-style (lands in R\&R) ===

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 5.0,  'desc': 'Specialist TRIAL PANEL FIT'},

&#x20;

&#x20;           # === CHECK labour (lands in EVA Check column) ===

&#x20;           {'type': 'check\_labour', 'guide': '1737', 'wu': 2.0, 'desc': 'L/DOOR MIRROR'},

&#x20;

&#x20;           # === Additional R+R "specialist" labour (lands in R\&R) ===

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist QC AND ROAD TEST'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 5.0,  'desc': 'Specialist PRE REPAIR CLEAN'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist WASH AND VACUUM'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'work test description'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist QC AND ROAD TEST'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 5.0,  'desc': 'Specialist PRE REPAIR CLEAN'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist POST REPAIR CHECK'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist PRE REPAIR CHECK'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist WASH AND VACUUM'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist STANDARD SHUTDOWN'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist SPECIALIST VALET'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 10.0, 'desc': 'Specialist YARD CHARGE'},

&#x20;           {'type': 'rnr', 'guide': '1000', 'wu': 1.0,  'desc': 'Corrosion Prote ADDITIONAL PANEL'},

&#x20;

&#x20;           # === PAINT ===

&#x20;           {'type': 'paint\_blend', 'guide': '221',  'wu': 170.0, 'desc': 'OUTER BODY'},

&#x20;           {'type': 'paint\_new',   'guide': '1000', 'wu': 3.0,   'desc': 'DENIB / POLISH'},

&#x20;           {'type': 'paint\_blend', 'guide': '1000', 'wu': 5.0,   'desc': 'MASKING TIME ADDIITONAL'},

&#x20;           {'type': 'paint\_prep',  'guide': '',     'wu': 13.0},

&#x20;

&#x20;           # === PARTS ===

&#x20;           {'type': 'new\_part', 'guide': '349',  'desc': 'FRT IMPACT DAMPER',  'part\_num': '93450026',   'price':   52.78},

&#x20;           {'type': 'new\_part', 'guide': '410',  'desc': 'GRILLE',             'part\_num': '93450928',   'price':  348.16},

&#x20;           {'type': 'new\_part', 'guide': '471',  'desc': 'BONNET',             'part\_num': '6500694680', 'price':  642.42},

&#x20;           {'type': 'new\_part', 'guide': '562',  'desc': 'RIGHT HEADLAMP ASSY','part\_num': '95527871',   'price':  460.36},

&#x20;           {'type': 'new\_part', 'guide': '1411', 'desc': 'WINDSCREEN BOND KIT','part\_num': '93165025',   'price':   94.58},

&#x20;           {'type': 'new\_part', 'guide': '1729', 'desc': 'L/F MOUNTING KIT',   'part\_num': 'USE SINGLE PARTS', 'price': 0.00},

&#x20;           {'type': 'new\_part', 'guide': '1731', 'desc': 'CLIP FASTENING',     'part\_num': '95508766',   'price':   24.48},

&#x20;           {'type': 'new\_part', 'guide': '1733', 'desc': 'SCREW',              'part\_num': '93452165',   'price':    6.20},

&#x20;           {'type': 'new\_part', 'guide': '1735', 'desc': 'SCREW',              'part\_num': '91169531',   'price':    2.88},

&#x20;           {'type': 'new\_part', 'guide': '1781', 'desc': 'L/SLIDING DOOR',     'part\_num': '93455820',   'price':  815.05},

&#x20;           {'type': 'new\_part', 'guide': '2353', 'desc': 'REAR ROOF',          'part\_num': '95518334',   'price': 2224.99},

&#x20;           {'type': 'new\_part', 'guide': '2357', 'desc': 'REAR ROOF FRAME',    'part\_num': '91160066',   'price':  247.67},

&#x20;           {'type': 'new\_part', 'guide': '2392', 'desc': 'RR ROOF REPAIR KIT', 'part\_num': '1699686780', 'price':   73.51},

&#x20;           {'type': 'new\_part', 'guide': '2963', 'desc': 'L/R LOWER DOOR STOP','part\_num': '93850442',   'price':   14.96},

&#x20;           {'type': 'new\_part', 'guide': '3297', 'desc': 'L/R REFLECTOR',      'part\_num': '9160858',    'price':   13.87},

&#x20;           {'type': 'new\_part', 'guide': '3482', 'desc': 'R/R SIDE PANEL',     'part\_num': '95518700',   'price': 1743.51},

&#x20;           {'type': 'new\_part', 'guide': '9637', 'desc': 'PARK SYS CONT UNIT', 'part\_num': '93868062',   'price':   83.09},

&#x20;           {'type': 'new\_part', 'guide': '1000', 'desc': 'BEAD SEALER',        'part\_num': 'Renew',      'price':   25.00, 'unpriced': True},

&#x20;           {'type': 'new\_part', 'guide': '1000', 'desc': 'BORON DRILLS (8MM)', 'part\_num': 'Renew',      'price':   72.87, 'unpriced': True},

&#x20;           {'type': 'new\_part', 'guide': '1000', 'desc': 'COOLANT / ATF',      'part\_num': 'Renew',      'price':   25.00, 'unpriced': True},

&#x20;

&#x20;           # === EXTRAS ===

&#x20;           {'type': 'specialist\_fixed', 'desc': 'ASSESSMENT FEE',                     'price': 176.96},

&#x20;           {'type': 'specialist\_fixed', 'desc': 'VEHICLE CARE KIT',                   'price':  10.41},

&#x20;           {'type': 'specialist\_fixed', 'desc': 'WHEEL ALIGNMENT', 'text': 'Check \& Adj toe', 'price': 174.88},

&#x20;           {'type': 'specialist\_fixed', 'desc': 'CORROSION PROTECTION MATERIALS EXTERNAL', 'price': 7.29},

&#x20;       ],

&#x20;

&#x20;       'notes': (

&#x20;           'Comprehensive damage assessment. Vehicle has suffered severe impact damage '

&#x20;           'across multiple panels. Full repair specification compiled. '

&#x20;           'AI CONFIDENCE: MEDIUM. Subject to engineer review and approval.'

&#x20;       ),

&#x20;   }

&#x20;

&#x20;   result = build\_pdf('/home/claude/work/ours\_v4.pdf', data)

&#x20;   print()

&#x20;   print('=== Compiled sections ===')

&#x20;   for section, rows in result\['compiled'].items():

&#x20;       print(f'  {section}: {len(rows)} rows')

&#x20;   print()

&#x20;   print('=== Key totals ===')

&#x20;   t = result\['totals']

&#x20;   print(f"  Labour WU:            {t\['labour\_wu']:.1f}  ({t\['labour\_hours']:.2f} hrs)")

&#x20;   print(f"  Labour cost:          £{t\['labour\_cost']:,.2f}")

&#x20;   print(f"  Paint WU:             {t\['paint\_wu']:.1f}   ({t\['paint\_hours']:.2f} hrs)")

&#x20;   print(f"  Paint cost:           £{t\['paint\_cost']:,.2f}")

&#x20;   print(f"  Total labour:         £{t\['total\_labour']:,.2f}")

&#x20;   print(f"  Total paint material: £{t\['total\_paint\_material']:,.2f}")

&#x20;   print(f"  Total parts:          £{t\['total\_parts']:,.2f}")

&#x20;   print(f"  Total extras:         £{t\['total\_extras']:,.2f}")

&#x20;   print(f"  Grand ex VAT:         £{t\['grand\_ex\_vat']:,.2f}")

&#x20;   print(f"  Grand inc VAT:        £{t\['grand\_inc\_vat']:,.2f}")

&#x20;   print(f"  Total pages:          {result\['total\_pages']}")

