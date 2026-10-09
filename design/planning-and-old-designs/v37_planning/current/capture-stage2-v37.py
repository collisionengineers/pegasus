"""Stage 2 conformance: the routed Management Reports page from the local
conformance host (artifacts/v37-stage2-host, https://localhost:5263, the
mockup's fixtures), shot in the same states and widths as Design A's
v37-shots and checked for the behaviours the notes promised."""
import json
import os
import re
from pathlib import Path

from playwright.sync_api import sync_playwright

current = Path(__file__).resolve().parent
out = current / 'stage2-shots'
out.mkdir(exist_ok=True)
base = 'https://localhost:5263/Administration/Reports'
widths = {1580: 1000, 1440: 900, 760: 1000}
# Shot number, state, query, widths: the numbers are v37-shots' for Design A.
states = [
    ('01', 'default', '', [1580, 1440, 760]),
    ('09', 'invalid', 'from=2026-10-09T09:41&to=2026-09-08T09:41', [1440]),
    ('13', 'principal-unavailable', 'from=2026-09-08T09:13&to=2026-10-09T09:41', [1440]),
    ('17', 'person', None, [1440]),
    ('21', 'empty', 'from=2026-09-08T09:21&to=2026-10-09T09:41', [1440]),
]
ok, fails = [0], []


def check(name, cond):
    if cond:
        ok[0] += 1
    else:
        fails.append(name)


with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, executable_path=os.environ.get('PEGASUS_CHROME') or None)
    context = browser.new_context(viewport={'width': 1440, 'height': 900}, ignore_https_errors=True, accept_downloads=True)
    page = context.new_page()
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))

    def load(query=''):
        page.goto(base + ('?' + query if query else ''))
        page.wait_for_load_state('networkidle')

    load()
    rkhan = page.locator('#report-engineer option', has_text='r.khan').get_attribute('value')
    for number, state, query, shot_widths in states:
        query = f'engineerId={rkhan}' if state == 'person' else query
        for width in shot_widths:
            page.set_viewport_size({'width': width, 'height': widths[width]})
            load(query)
            page.screenshot(path=str(out / f'{number}-stage2-{state}-{width}.png'))
            spill = page.evaluate('() => document.documentElement.scrollWidth - window.innerWidth')
            check(f'{state}@{width}: nothing spills sideways ({spill}px)', spill <= 0)
    page.set_viewport_size({'width': 1580, 'height': 1000})
    load()
    page.screenshot(path=str(out / '34-stage2-full-1580.png'), full_page=True)
    page.set_viewport_size({'width': 1440, 'height': 900})

    # Design A: the sections in order, no MI label, the Case list note only.
    load()
    titles = page.locator('main h2, .stack > section h2').all_inner_texts()
    check(f'sections in Design A order {titles}', titles[:7] == ['Engineer activity', 'Reports by Principal', 'By month', 'Outcomes', 'Turnaround', 'Queues', 'Case list'])
    check('no MI label', 'MI01' not in page.content())
    check('only the Case list note', page.locator('.admin-report-note').count() == 1)
    values = page.locator('.metric-value').all_inner_texts()
    check(f'Reports sent agrees across reports (item J) {values[:6]}', values[1] == '110' and values[4] == '110')
    check('Automation row', page.locator('table tbody tr', has_text='Automation').count() == 1)
    check('previous period under the totals (item G)', page.locator('.metric-meta', has_text='Previous period').count() == 6)
    check('Person lists the people with activity (item N)', page.locator('#report-engineer option').count() == 6)

    # Sort (item P): a count's first click is largest first, one arrow.
    page.locator('th a', has_text='Reports sent').first.click()
    page.wait_for_load_state('networkidle')
    first = page.locator('table').first.locator('tbody tr').first.locator('td').first.inner_text()
    check(f'first click on Reports sent puts j.okafor first ({first})', first == 'j.okafor')
    check('aria-sort descending', page.locator('th[aria-sort="descending"]').count() == 1)
    arrow = page.evaluate("() => getComputedStyle(document.querySelector('th[aria-sort] a'), '::after').content")
    check(f'one arrow, drawn by site.css ({arrow})', '↓' in arrow and '↓' not in page.locator('th[aria-sort] a').inner_text())

    # Work (item D): the choice submits itself and keeps the sort.
    with page.expect_navigation():
        page.select_option('#report-work', 'audit')
    page.wait_for_load_state('networkidle')
    check('Work keeps the sort', 'sort=reports' in page.url and 'work=audit' in page.url)
    qdos = page.locator('table', has=page.locator('th', has_text='Agreed fees')).first.locator('tbody tr', has_text='QDOS').locator('td').nth(1).inner_text().strip()
    check(f'Work Audit shows QDOS Audit reports produced (13, got {qdos})', qdos == '13')

    # Period (item H): typing a date makes the period Custom.
    load('period=last-month')
    check('Last month chosen', page.locator('#report-period').input_value() == 'last-month')
    page.fill('#report-from', '2026-09-01T00:00')
    check('typing a date makes the period Custom', page.locator('#report-period').input_value() == 'custom')

    # Downloads (item L): busy then done; a failed report refuses only its own CSV.
    load()
    with page.expect_download():
        page.locator('a[href*="handler=Csv"]').first.click()
    check('a download says Downloaded', 'Downloaded' in page.locator('a[href*="handler=Csv"]').first.inner_text())
    load('from=2026-09-08T09:13&to=2026-10-09T09:41')
    with page.expect_download():
        page.locator('a[href*="handler=Csv"]').first.click()
    check('Engineer activity CSV still downloads when Reports by Principal failed', page.locator('.toast--danger').count() == 0)
    page.locator('a[href*="handler=PrincipalCsv"]').first.click()
    page.wait_for_timeout(1500)
    check('the failed report refuses its own CSV', page.locator('.toast--danger').count() == 1)

    # Invalid period (item B2): the error in the bar, no report, no figure.
    load('from=2026-10-09T09:41&to=2026-09-08T09:41')
    check('invalid period error', page.locator('.admin-report-period', has_text='Choose a valid date range.').count() == 1)
    check('no figure for an invalid period', page.locator('.metric-value').count() == 0)
    check('the Case list stays', page.locator('[data-case-list]').count() == 1)
    check('no script error', not errors)
    browser.close()

result = {'fail': fails, 'okCount': ok[0]}
(out / 'verification.json').write_text(json.dumps({'date': '2026-10-09', 'host': base, 'result': result}, indent=2), encoding='utf-8')
print('RESULT', json.dumps(result))
