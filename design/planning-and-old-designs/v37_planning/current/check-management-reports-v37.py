"""Offline evidence for the v37 Management Reports mockups (not application evidence).

Drives the live baseline and designs A, B and C through every page state, the
sort, Work, Person, Report, Principal, download and Case list flows, the copy
allow-list and the geometry at 1580, 1440 and 760, prints
RESULT {"fail":[],"okCount":N}, then captures the screenshot set.
`--no-shots` stops after the RESULT line. Set PEGASUS_CHROME to a Chromium
build when the Python driver expects a different one.
"""
import json
import os
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from playwright.sync_api import sync_playwright

current = Path(__file__).resolve().parent
shots = current / 'v37-shots'
shots.mkdir(exist_ok=True)
designs = ['live', 'a', 'b', 'c']
presets = ['default', 'empty', 'person', 'engineer-unavailable', 'principal-unavailable', 'monthly-unavailable', 'invalid', 'preset', 'refused', 'busy', 'done', 'proposals']
widths = [(1580, 1000), (1440, 900), (760, 1000)]
fails, ok = [], [0]
errors, external = [], []
banned = ['intake', 'bounded', 'projection', 'lease', 'opaque', 'ingress', 'composed', 'artifact', 'durable', 'aggregate', 'caller', 'correlation identifier', 'bytes', 'mi-0', 'dispute', 'disputes']

# Copy allow-list. Live: the words Reports.cshtml, _AdminNav, _PageHeader,
# OperatorLabels (Admin, CaseList, Busy) and CaseListColumns put on the page
# today. Proposal: each new word belongs to the lettered item that asks for it.
fixtures_text = (current / 'lib' / 'reports-fixtures.mjs').read_text(encoding='utf-8')
column_titles = set(re.findall(r"\['[a-z_.]+', '([^']+)'\]", fixtures_text)) | {t.replace("\\'", "'") for t in re.findall(r"sided\('[a-z_]+', '((?:[^'\\]|\\.)+)'\)", fixtures_text)}
live_words = {
    'Management Reports', 'Administration', 'Engineer activity', 'MI01', 'MI02', 'MI03', 'MI04', 'From', 'To', 'Person', 'All people', 'Apply',
    'Download CSV', 'Download workbook', 'Downloading…', 'Downloaded', 'Queries received', 'Reports sent', 'Audit reports sent',
    'Amendment requests', 'Received to sent', 'Unavailable', 'No engineer activity was recorded for this period.', 'Reports by Principal',
    'Reports produced', 'Agreed fees', 'Principal', 'By month', 'Month', 'Fee notes produced', 'No reports were recorded for this period.',
    'Turnaround', 'Cases currently held', 'Currently held', 'Oldest held since', 'Time to produce', 'Time to ready', 'Time to send',
    'No holding or turnaround activity was recorded for this period.', 'Case list', 'Preset', 'No preset', 'Use preset', 'Received from',
    'Received to', 'All time', 'Include Triage Cases', 'Case', 'Outcomes', 'Engineers', 'Claim and vehicle', 'Money', 'Parties and activity',
    'Preset name', 'Save as new preset', 'Save preset', 'Remove preset', 'Choose a valid date range.', 'Choose at least one column.',
    'Queries received are credited to the Engineer assigned to the Case. Reports sent are credited to the staff member recorded as the sender.',
    'One row per Case received in the period, open or closed. N/A means the column does not apply to that Case type; a blank cell means nothing is recorded yet.',
    'The file could not be downloaded. Try again.', 'The preset was saved.', 'The preset was updated.', 'The preset was removed.',
    'Enter a preset name of up to 100 characters.', 'Another preset already has that name.', '—', '↑', '↓',
    # Shell: rail, utility bar, admin nav and the two dialogs, from the live layout.
    'PEGASUS', 'Case management', 'Work Centre', 'Inbox', 'Upload', 'Cases', 'Search', 'Work', 'Manage', 'Collapse', 'Skip to main content', 'Ctrl K',
    'New case', 'Current · 09:41', 'alex', 'Administrator', 'A', 'People and access', 'Staff accounts & roles', 'Contacts', 'Configuration',
    'Workflow configuration', 'Mail settings', 'E-mail templates', 'Valuation presets', 'Operations and oversight', 'Service health', 'Logs',
    'Release notes', 'Problem reports', 'AI jobs', 'Automation & AI',
} | column_titles | {'Inspection', 'Audit'}
sided_bases = {t.replace("\\'", "'") for t in re.findall(r"sided\('[a-z_]+', '((?:[^'\\]|\\.)+)'\)", fixtures_text)} | {'Reports produced', 'Reports sent', 'Agreed fees'}
live_words |= {f'{base} · {side}' for base in sided_bases for side in ('Inspection', 'Audit')}
proposal_words = {
    'B': {'Period'},
    'D': {'All'},
    'E': {'Queues', 'Now', 'Triages', 'Unidentified', 'Oldest Triage since'},
    'F': {'Cases by stage', 'Not ready', 'Review', 'With Engineer', 'Held', 'Query', 'Total'},
    'G': set(),
    'H': {'This month', 'Last month', 'This quarter', 'Last 12 months', 'Custom', 'Last 6 months', 'This year'},
    'O': {'Outcomes', 'Repairable', 'Total loss', 'Cash in lieu', 'Contract repair', 'Agrees', 'Differs'},
    'J': {'Automation'},
    'A-B': {'Report'},
    'A-C': {'Measure', 'Total'},
}
allowed_exact = live_words | {w for words in proposal_words.values() for w in words}
fixture_names = {'alex', 'd.ward', 'j.okafor', 'm.lewis', 'r.khan', 's.patel', 'ALPHA', 'PCH', 'QDOS', 'ROUTE', 'Monthly invoicing', 'Total losses'}
allowed_patterns = [
    re.compile(r'^\d+$'), re.compile(r'^£[\d,]+\.\d\d$'), re.compile(r'^\d+ days?$'), re.compile(r'^\d\d [A-Z][a-z]{2} \d{4} \d\d:\d\d$'),
    re.compile(r'^[A-Z][a-z]{2} \d{4}$'),
    re.compile(r'^Previous period (\d+|£[\d,]+\.\d\d)$'),  # item G
    re.compile(r'^Oldest since \d\d [A-Z][a-z]{2} \d{4} \d\d:\d\d$'),  # item E
]
used_items = set()


def word_allowed(text):
    if text in live_words or text in fixture_names:
        return True
    for item, words in proposal_words.items():
        if text in words:
            used_items.add(item)
            return True
    for pattern in allowed_patterns:
        if pattern.match(text):
            if text.startswith('Previous period'):
                used_items.add('G')
            return True
    return False


def check(name, cond):
    if cond:
        ok[0] += 1
    else:
        fails.append(name)


def url(design, query):
    return (current / f'pegasus_management_reports_{design}_v37.html').as_uri() + ('?' + query if query else '')


TEXTS = """() => {
  const out = []; const root = document.querySelector('.app-shell');
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  while (walker.nextNode()) {
    const n = walker.currentNode; const el = n.parentElement;
    if (!el || el.closest('script,style,[hidden],.sr-only,option:not(:checked)')) continue;
    const t = n.textContent.replace(/\\s+/g, ' ').trim(); if (t) out.push(t);
  }
  document.querySelectorAll('.app-shell option').forEach(o => out.push(o.textContent.trim()));
  return out;
}"""
SECTIONS = "() => [...document.querySelectorAll('[data-section]')].map(s => s.getAttribute('data-section'))"


with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, executable_path=os.environ.get('PEGASUS_CHROME') or None, args=['--allow-file-access-from-files'])
    context = browser.new_context(viewport={'width': 1440, 'height': 900}, device_scale_factor=1)

    def block(route):
        external.append(route.request.url)
        route.abort()

    context.route('http://**', block)
    context.route('https://**', block)
    page = context.new_page()
    page.on('pageerror', lambda e: errors.append(f'pageerror: {e}'))
    page.on('console', lambda m: errors.append(f'console: {m.text}') if m.type == 'error' else None)

    def load(design, query='', width=1440, height=900):
        page.set_viewport_size({'width': width, 'height': height})
        page.goto(url(design, query))
        page.wait_for_function('window.mockupReady === true')

    def text(selector):
        return page.locator(selector).first.inner_text().strip()

    # ---- every design × every preset: frame, copy, sections, geometry ----
    for design in designs:
        for preset in presets:
            tag = f'{design}/{preset}'
            load(design, f'state={preset}&embed=1')
            check(f'{tag}: one H1', page.locator('h1').count() == 1 and text('h1') == 'Management Reports')
            check(f'{tag}: eyebrow', page.locator('.page-title .eyebrow').text_content().strip() == 'Administration')
            check(f'{tag}: Management Reports is the current admin area', page.locator('.admin-nav a[aria-current="page"]').inner_text().strip() == 'Management Reports')
            check(f'{tag}: strip hidden by embed', page.locator('[data-mock]').is_hidden())
            texts = page.evaluate(TEXTS)
            stray = sorted({t for t in texts if not word_allowed(t)})
            check(f'{tag}: every word is a live label, a fixture or a lettered proposal word {stray[:6]}', not stray)
            low = ' '.join(texts).lower()
            hit = [b for b in banned if re.search(r'\b' + re.escape(b) + r'\b', low)]
            check(f'{tag}: no banned word {hit}', not hit)
            check(f'{tag}: no green status chip', page.locator('#rp-root .status--green').count() == 0)
            sections = page.evaluate(SECTIONS)
            if design == 'live':
                check(f'{tag}: four live sections in order', [s for s in sections if s != 'months'] == ['mi01', 'mi02', 'mi03', 'caselist'])
                check(f'{tag}: live notes drawn', page.locator('.admin-report-note').count() == 2)
            else:
                check(f'{tag}: period bar first', page.locator('#rp-root [data-period-form]').count() == 1)
                check(f'{tag}: workbook in the page head', page.locator('.page-actions [data-download="workbook"]').count() == 1)
                check(f'{tag}: only the Case list note by default (item C)', page.locator('.admin-report-note').count() == (1 if 'caselist' in sections else 0) and page.locator('[data-section="mi01"] .admin-report-note').count() == 0)
                check(f'{tag}: no MI label by default', 'MI01' not in texts and 'MI02' not in texts)
            # Unavailable is never a zero.
            for section, flag in (('mi01', 'engineer-unavailable'), ('mi02', 'principal-unavailable'), ('mi03', 'principal-unavailable')):
                if preset == flag and section in sections:
                    values = page.locator(f'[data-section="{section}"] .metric-value').all_inner_texts()
                    # With Queues on, Turnaround has no tile: its table row says Unavailable.
                    shown = page.locator(f'[data-section="{section}"] tbody').first.inner_text()
                    check(f'{tag}: {section} shows Unavailable, not 0', all(v == 'Unavailable' for v in values) and 'Unavailable' in shown)
            if preset == 'invalid':
                check(f'{tag}: the period error is shown', 'Choose a valid date range.' in texts)
                if design == 'live':
                    zeros = page.locator('[data-section="mi01"] .metric-value').all_inner_texts()
                    check(f'{tag}: live keeps its false zero (finding 8)', zeros == ['0', '0', '0'])
                else:
                    check(f'{tag}: no report drawn for an invalid period', sections == ['caselist'])
                    check(f'{tag}: no zero figure anywhere', page.locator('#rp-root .metric-value').count() == 0)
            if preset == 'proposals' and design in ('a', 'c'):
                for s in ('queues', 'stages', 'outcomes'):
                    check(f'{tag}: {s} drawn', s in sections)
                check(f'{tag}: held moves to Queues', page.locator('[data-section="mi03"] th', has_text='Currently held').count() == 0)
                check(f'{tag}: previous period beside the totals', page.locator('.metric-meta', has_text='Previous period').count() >= 3)
            for width, height in widths:
                page.set_viewport_size({'width': width, 'height': height})
                spill = page.evaluate('() => document.documentElement.scrollWidth - window.innerWidth')
                check(f'{tag}@{width}: nothing spills sideways ({spill}px)', spill <= 0)
            page.set_viewport_size({'width': 1440, 'height': 900})

    # ---- coverage: every live control and fact has a place in each design ----
    live_headers = ['Person', 'Queries received', 'Amendment requests', 'Reports sent', 'Audit reports sent', 'Received to sent']
    for design in designs:
        views = ['state=default&embed=1'] if design != 'b' else [f'report={r}&embed=1' for r in ('engineers', 'principals', 'months', 'turnaround', 'queues', 'caselist')]
        seen = []
        for v in views:
            load(design, v)
            seen += page.evaluate(TEXTS)
            seen += page.locator('#rp-root input, #rp-root select').evaluate_all('els => els.map(e => "#" + (e.id || e.name))')
        joined = set(seen)
        for h in live_headers:
            check(f'{design}: Engineer activity column "{h}"', any(h == s or s.startswith(h + ' ') for s in joined))
        for f in ['Reports produced', 'Reports sent', 'Agreed fees', 'Fee notes produced', 'Time to produce', 'Time to ready', 'Time to send', 'Oldest held since', 'Cases currently held']:
            check(f'{design}: fact "{f}"', f in joined)
        for c in ['#report-from', '#report-to', '#case-list-preset', '#case-list-from', '#case-list-to', '#case-list-preset-name', '#AllTime', '#IncludeTriage']:
            check(f'{design}: control {c}', c in joined)
        check(f'{design}: Person filter', '#report-engineer' in joined or '#rp-engineerId' in joined)
        for b in ['Apply', 'Download CSV', 'Download workbook', 'Use preset', 'Save as new preset']:
            check(f'{design}: button "{b}"', b in joined)
        for legend in ['Case', 'Outcomes', 'Engineers', 'Claim and vehicle', 'Money', 'Parties and activity']:
            check(f'{design}: Case list group "{legend}"', legend in joined)

    # ---- flows ----
    # Sort: live's first click on a count sorts smallest first; a proposal's largest first.
    for design in designs:
        load(design, 'embed=1')
        check(f'{design}: no sort arrow before a column is chosen', page.locator('[data-engineer-activity] th[aria-sort]').count() == 0)
        page.locator('[data-section="mi01"] [data-sort="reports"]').click()
        first = page.locator('[data-engineer-activity] tbody tr').first.locator('td').first.inner_text()
        expected, announced = ('m.lewis', 'ascending') if design == 'live' else ('j.okafor', 'descending')
        check(f'{design}: first click on Reports sent puts {expected} first ({first})', first == expected)
        check(f'{design}: sort state is announced', page.locator(f'[data-engineer-activity] th[aria-sort="{announced}"]').count() == 1)
        spans = page.locator('[data-engineer-activity] [data-sort-arrow]').all_inner_texts()
        check(f'{design}: arrow drawn once by site.css, twice live (finding 13)', ('↑' in spans) == (design == 'live'))
    # Person: one row; the other reports unchanged.
    for design in designs:
        load(design, 'state=person&embed=1' + ('&report=engineers' if design == 'b' else ''))
        rows = page.locator('[data-engineer-activity] tbody tr').count()
        check(f'{design}: Person r.khan leaves one row', rows == 1)
    # Person choices (item N).
    load('a', 'embed=1')
    check('a: Person lists people with activity', page.locator('#rp-engineerId option').count() == 6)
    load('a', 'opt=person:all&embed=1')
    check('a: Person lists every enabled account (live variant)', page.locator('#rp-engineerId option').count() == 7)
    # Work (item D): Audit narrows the figures; columns variant restores ten columns.
    for design in ('a', 'b'):
        load(design, 'work=audit&embed=1' + ('&report=principals' if design == 'b' else ''))
        qdos = page.locator('[data-section="mi02"] tbody tr', has_text='QDOS').locator('td').nth(1).inner_text().strip()
        check(f'{design}: Work Audit shows QDOS Audit reports produced (13)', qdos == '13')
        load(design, 'opt=work:columns&embed=1' + ('&report=principals' if design == 'b' else ''))
        check(f'{design}: columns variant has ten columns', page.locator('[data-section="mi02"] thead th').count() == 10)
    load('a', 'embed=1')
    check('a: Reports by Principal four columns', page.locator('[data-section="mi02"] thead th').count() == 4)
    check('a: By month six columns', page.locator('[data-section="months"] thead th').count() == 6)
    # Totals agree with their rows.
    check('a: Reports produced total 112', page.locator('[data-section="mi02"] .metric-value').first.inner_text() == '112')
    check('a: Agreed fees total £19,835.00', page.locator('[data-section="mi02"] .metric-value').nth(2).inner_text() == '£19,835.00')
    check('a: MI-01 Reports sent agrees with MI-02 at 110, Automation its own row (item J)', page.locator('[data-section="mi01"] .metric-value').nth(1).inner_text() == '110' and page.locator('[data-section="mi02"] .metric-value').nth(1).inner_text() == '110' and page.locator('[data-engineer-activity] tbody tr', has_text='Automation').count() == 1)
    load('live', 'embed=1')
    check('live: MI-01 Reports sent 107, Automation left out', page.locator('[data-section="mi01"] .metric-value').nth(1).inner_text() == '107' and page.locator('[data-engineer-activity] tbody tr', has_text='Automation').count() == 0)
    # B: a tile opens its report and is pressed.
    load('b', 'embed=1')
    check('b: Engineer activity by default', page.evaluate(SECTIONS) == ['mi01'])
    page.locator('.rp-overview [data-report="months"]').click()
    check('b: Agreed fees tile opens By month', page.evaluate(SECTIONS) == ['months'])
    check('b: the tile is pressed', page.locator('.rp-overview [aria-pressed="true"]').inner_text().startswith('Agreed fees'))
    page.select_option('#rp-report', 'caselist')
    check('b: Report choice opens the Case list', page.evaluate(SECTIONS) == ['caselist'])
    # C: choosing a Principal opens its months.
    load('c', 'embed=1')
    check('c: QDOS chosen by default', text('#detail-title') == 'QDOS')
    page.locator('[data-principal="PCH"]').click()
    check('c: choosing PCH opens PCH', text('#detail-title') == 'PCH' and page.locator('.rp-ledger tr[aria-selected="true"]').count() == 1)
    check('c: ledger has six months, Principal and Total', page.locator('.rp-ledger thead th').count() == 8)
    check('c: month bars drawn (item I)', page.locator('.rp-bar').count() == 6)
    page.select_option('#rp-measure', 'produced')
    total = page.locator('.rp-ledger tfoot tr').last.locator('td').last.inner_text()
    check(f'c: Reports produced over six months ({total})', total == '444')
    load('c', 'opt=bars:off&embed=1')
    check('c: bars switch off', page.locator('.rp-bar').count() == 0)
    # Downloads: busy then done; a failed section blocks only its own CSV in a proposal.
    for design in designs:
        load(design, 'embed=1' + ('&report=principals' if design == 'b' else ''))
        button = page.locator('[data-section="mi02"] [data-download="csv-mi02"]') if design != 'c' else page.locator('[data-download="csv-mi02"]')
        button.click()
        check(f'{design}: a download says Downloading…', 'Downloading…' in button.inner_text())
        page.wait_for_timeout(900)
        check(f'{design}: then Downloaded', 'Downloaded' in button.inner_text())
    load('live', 'state=principal-unavailable&embed=1')
    page.locator('[data-section="mi01"] [data-download="csv-mi01"]').click()
    check('live: one failed section blocks Engineer activity CSV (finding 9)', page.locator('.toast--danger', has_text='The file could not be downloaded. Try again.').count() == 1)
    load('a', 'state=principal-unavailable&embed=1')
    page.locator('[data-section="mi01"] [data-download="csv-mi01"]').click()
    check('a: Engineer activity CSV still downloads (item L)', page.locator('.toast--danger').count() == 0)
    page.locator('[data-section="mi02"] [data-download="csv-mi02"]').click()
    check('a: the unavailable section refuses its own CSV', page.locator('.toast--danger').count() == 1)
    # Case list: a preset ticks its columns; no column refuses.
    for design in designs:
        load(design, 'embed=1' + ('&report=caselist' if design == 'b' else ''))
        page.select_option('#case-list-preset', label='Monthly invoicing')
        page.locator('[data-case-preset-form] button').click()
        ticked = page.locator('[data-case-list-form] input[name="Columns"]:checked').count()
        check(f'{design}: Monthly invoicing ticks its eight columns', ticked == 8)
        check(f'{design}: Save preset and Remove preset appear', page.locator('[data-case-action="remove"]').count() == 1)
    load('live', 'state=refused&embed=1')
    check('live: refusal notice', page.locator('[data-section="caselist"] .notice--danger').inner_text() == 'Choose at least one column.')
    # Apply with From after To.
    load('a', 'embed=1')
    page.fill('#report-from', '2026-10-09T09:41')
    page.fill('#report-to', '2026-09-08T09:41')
    page.locator('[data-period-form] button[type="submit"]').click()
    page.wait_for_function('window.mockupReady === true')
    check('a: Apply with From after To shows the period error', 'Choose a valid date range.' in page.evaluate(TEXTS))
    # The strip: design nav and every option.
    page.goto(url('a', ''))
    page.wait_for_function('window.mockupReady === true')
    check('strip: four design links', page.locator('[data-mock] nav a').count() == 4)
    check('strip: one picker per lettered variable', page.locator('[data-option-picker]').count() == 10)
    page.goto(url('live', ''))
    page.wait_for_function('window.mockupReady === true')
    check('strip: Today has no proposal pickers', page.locator('[data-option-picker]').count() == 0)

    check('no console errors', not errors)
    check('no external requests', not external)
    unused = sorted(set(proposal_words) - used_items - {'G'})
    check(f'every proposal word list is used {unused}', not unused)
    result = {'fail': fails, 'okCount': ok[0]}
    print('RESULT ' + json.dumps(result), flush=True)
    if errors:
        print('\n'.join(errors[:10]))
    if fails or '--no-shots' in sys.argv:
        browser.close()
        raise SystemExit(1 if fails else 0)

    # ---- screenshots ----
    shot_list = []
    n = [0]

    def shoot(name, design, query, sizes=((1440, 900),), full=False):
        n[0] += 1
        for width, height in sizes:
            load(design, query + '&embed=1' if query else 'embed=1', width, height)
            file = f'{n[0]:02d}-{design}-{name}-{width}.png'
            page.screenshot(path=str(shots / file), full_page=full)
            shot_list.append(file)

    all_sizes = widths
    for d in ('a', 'b', 'c'):
        shoot('default', d, '', all_sizes)
    shoot('default', 'live', '', all_sizes)
    for d in ('a', 'b', 'c'):
        shoot('proposals', d, 'state=proposals', all_sizes)
    for d in ('live', 'a', 'b', 'c'):
        shoot('invalid', d, 'state=invalid')
    for d in ('live', 'a', 'b', 'c'):
        shoot('principal-unavailable', d, 'state=principal-unavailable')
    for d in ('live', 'a', 'b', 'c'):
        shoot('person', d, 'state=person')
    for d in ('live', 'a', 'b', 'c'):
        shoot('empty', d, 'state=empty')
    for r in ('principals', 'months', 'turnaround', 'caselist'):
        shoot(f'report-{r}', 'b', f'report={r}')
    shoot('principal-pch', 'c', 'principal=PCH')
    shoot('work-columns', 'a', 'opt=work:columns')
    shoot('preset', 'live', 'state=preset')
    shoot('busy', 'a', 'state=busy')
    shoot('done', 'a', 'state=done')
    for d in ('live', 'a', 'b', 'c'):
        n[0] += 1
        load(d, 'state=proposals&embed=1' if d != 'live' else 'embed=1', 1580, 1000)
        file = f'{n[0]:02d}-{d}-full-1580.png'
        page.screenshot(path=str(shots / file), full_page=True)
        shot_list.append(file)
    browser.close()

(shots / 'verification.json').write_text(json.dumps({
    'checkedAtUtc': datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
    'scope': 'Offline v37 mockup self-check. Not application evidence.',
    'result': result, 'screenshots': shot_list, 'errors': errors,
}, indent=2) + '\n', encoding='utf-8')
print(f'{len(shot_list)} screenshots written to {shots.name}/')
