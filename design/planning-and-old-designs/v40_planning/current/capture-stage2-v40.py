"""Stage 2 conformance: the routed Contacts pages from a local conformance host
(a scratch Kestrel host over the integration-test database, https://localhost:5271,
seeded with the mockup's Riverside, Accident Specialists, Eastfield, Pennine and
the long Notes on every Case on DFD), shot in the same states and widths as
v40-shots and checked for the behaviours the notes promised.

Run: python capture-stage2-v40.py <path to host.json>"""
import json
import os
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

current = Path(__file__).resolve().parent
out = current / 'stage2-shots'
out.mkdir(exist_ok=True)
host = json.loads(Path(sys.argv[1]).read_text())
base = host['url']
contacts = base + '/Administration/Contacts'
edit = lambda key, tab=None: f"{contacts}/Edit/{host[key]}" + (f'?tab={tab}' if tab else '')
heights = {1580: 1000, 1440: 900, 760: 1000}
ok, fails = [0], []


def check(name, cond):
    if cond:
        ok[0] += 1
    else:
        fails.append(name)


with sync_playwright() as p:
    chrome = os.path.join(os.environ['LOCALAPPDATA'], 'ms-playwright', 'chromium-1234', 'chrome-win64', 'chrome.exe')
    browser = p.chromium.launch(headless=True, executable_path=chrome)
    context = browser.new_context(viewport={'width': 1580, 'height': 1000}, ignore_https_errors=True)
    page = context.new_page()
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))

    def load(url):
        page.goto(url)
        page.wait_for_load_state('networkidle')

    def shoot(number, name, url, full=True, before=None):
        for width, height in heights.items():
            page.set_viewport_size({'width': width, 'height': height})
            load(url)
            if before:
                before()
            page.screenshot(path=str(out / f'{number}-{name}-{width}.png'))
            spill = page.evaluate('() => document.documentElement.scrollWidth - window.innerWidth')
            check(f'{name}@{width}: nothing spills sideways ({spill}px)', spill <= 0)
        if full:
            page.set_viewport_size({'width': 1580, 'height': 1000})
            load(url)
            if before:
                before()
            page.screenshot(path=str(out / f'{number}-{name}-full.png'), full_page=True)

    # 01 list: one panel per type in order, no Sort, no lede, no Type column.
    shoot('01', 'list', contacts)
    headings = page.locator('.admin-layout h2').all_inner_texts()
    order = [h for h in headings if h in ('Principals', 'Claim Sources', 'Repairers', 'Storage', 'Third Party Engineers')]
    check(f'list: panels in type order {order}', order == ['Principals', 'Claim Sources', 'Repairers', 'Storage', 'Third Party Engineers'])
    check('list: no Sort control', page.locator('select[name=Sort]').count() == 0)
    check('list: no lede', page.locator('#contacts-title + p, .panel-title-meta').count() == 0)
    check('list: no Type column', page.locator('th', has_text='Type').count() == 0)
    principal_heads = page.locator('section[aria-labelledby=contacts-Principal-title] th').all_inner_texts()
    check(f'list: Principal columns {principal_heads}', [h.strip().lower() for h in principal_heads[:7]] == ['principal code', 'name', 'contact person', 'email', 'phone', 'last case', 'status'])
    repairer_heads = page.locator('section[aria-labelledby=contacts-Repairer-title] th').all_inner_texts()
    check(f'list: Repairer columns {repairer_heads}', [h.strip().lower() for h in repairer_heads[:8]] == ['name', 'contact person', 'email', 'phone', 'postcode', 'linked principals', 'last case', 'status'])
    claim_heads = page.locator('section[aria-labelledby=contacts-ClaimSource-title] th').all_inner_texts()
    check(f'list: Claim Source columns {claim_heads}', [h.strip().lower() for h in claim_heads[:7]] == ['name', 'contact person', 'email', 'phone', 'linked principals', 'last case', 'status'])
    names = page.locator('section[aria-labelledby=contacts-Principal-title] tbody tr td:nth-child(2)').all_inner_texts()
    check('list: Principals sorted by name', names == sorted(names, key=str.lower))

    # 03 one type: Type narrows to one panel.
    shoot('03', 'list-single', contacts + '?Type=Principal', full=False)
    check('list-single: one panel', page.locator('section[aria-labelledby^=contacts-][aria-labelledby$=-title]:not([aria-labelledby=contacts-title])').count() == 1)
    # 04 search across panels.
    shoot('04', 'list-search', contacts + '?Search=Riverside', full=False)
    check('list-search: Repairers only', page.locator('h2', has_text='Repairers').count() == 1 and page.locator('h2', has_text='Principals').count() == 0)
    # 05 nothing found.
    shoot('05', 'list-empty', contacts + '?Search=Zzzz', full=False)
    check('list-empty: No contacts found.', page.get_by_text('No contacts found.').count() == 1)

    # 06-07 Add contact dialogs.
    shoot('06', 'dialog-type', contacts, full=False, before=lambda: page.click('[data-dialog-open=contact-type-dialog]'))
    shoot('07', 'dialog-new-principal', contacts + '?createType=Principal', full=False)
    check('dialog: Phone label', page.locator('#contact-create-form label[for=Telephone]').inner_text().strip() == 'Phone')
    check('dialog: no sentence', page.get_by_text('Enter the details for this').count() == 0)

    # 12 Principal, Contact tab (tabs layout); the notes box fits its text.
    shoot('12', 'principal-tabs', edit('dfd'))
    tabs = page.locator('nav.tabs a.tab').all_inner_texts()
    check(f'principal: tabs {tabs}', [t.strip() for t in tabs] == ['Contact', 'Report generation', 'Report sending', 'Salvage matrix', 'Default inspection location', 'Principal API'])
    check('principal: Contact tab current', page.locator('nav.tabs a[aria-current=page]').inner_text().strip() == 'Contact')
    check('principal: no Principal settings panel', page.get_by_text('Principal settings').count() == 0)
    for box in ('#contact-type', '#PrincipalCode', '#principal-inspection-mode', '#principal-accepted-identities'):
        check(f'principal: {box} is read-only', page.locator(box).get_attribute('readonly') is not None)
    fit = page.evaluate("() => { const a = document.querySelector('[data-notes-on-every-case] textarea'); return a.scrollHeight - a.clientHeight; }")
    check(f'principal: notes show every line ({fit}px hidden)', fit <= 1)
    widths = page.evaluate("() => ['#Telephone', '#ContactPerson'].map(s => document.querySelector(s).getBoundingClientRect().height)")
    check(f'principal: Phone box matches its neighbour {widths}', abs(widths[0] - widths[1]) < 0.5)
    border = page.evaluate("() => getComputedStyle(document.querySelector('.contact-editor > fieldset')).borderTopStyle")
    check(f'principal: no fieldset border ({border})', border == 'none')
    check('principal: Replace code below the tabs', page.locator('[data-dialog-open=replace-principal]').count() == 1)
    check('principal: no Guidance for new Cases', page.get_by_text('Guidance for new Cases').count() == 0)

    # 13 Report sending tab; 14 its form; 09 Report generation heading.
    shoot('13', 'principal-tabs-sending', edit('dfd', 'report-sending'))
    kinds = page.locator('[data-rs-kind]').first.locator('option').all_inner_texts()
    check(f'sending: condition kinds {kinds}', kinds[1:] == ['Claim Source', 'Repairer', 'Images from', 'Instruction mentions', 'Sender is not', 'Outcome', 'Bodyshop mentions'])
    shoot('09', 'principal-report-generation', edit('dfd', 'report-generation'), full=False)
    check('generation: heading', page.locator('#principal-report-title').inner_text().strip() == 'Report generation')

    # 15 a save comes back to its tab with the notice inside the panel.
    page.set_viewport_size({'width': 1580, 'height': 1000})
    load(edit('dfd', 'report-sending'))
    page.click('[data-report-sending-editor] button[type=submit]')
    page.wait_for_load_state('networkidle')
    check('saved: back on Report sending', 'tab=report-sending' in page.url)
    check('saved: notice inside the panel', page.locator('[data-report-sending-editor] .notice--success').count() == 1)
    page.screenshot(path=str(out / '15-principal-saved-1580.png'))
    page.screenshot(path=str(out / '15-principal-saved-full.png'), full_page=True)

    # 16 a refused salvage save shows at the top of its panel and keeps the typed rows.
    load(edit('dfd', 'salvage-matrix'))
    row = page.locator('[data-salvage-bands=S] tr').first
    row.locator('input[name=SalvageBandFrom]').fill('5000.00')
    row.locator('input[name=SalvageBandTo]').fill('2500.00')
    row.locator('input[name=SalvageBandPercentage]').fill('20')
    page.click('[data-salvage-matrix-editor] button[type=submit]')
    page.wait_for_load_state('networkidle')
    summary = page.locator('[data-salvage-matrix-editor] .validation-summary-errors').inner_text()
    check(f'refused: message at the top of the panel ({summary.strip()})', 'Cat S: From must not be more than To.' in summary)
    check('refused: typed rows kept', page.locator('input[name=SalvageBandFrom][value="5000.00"]').count() == 1)
    page.screenshot(path=str(out / '16-principal-refused-1580.png'))
    page.screenshot(path=str(out / '16-principal-refused-full.png'), full_page=True)

    # 19-23 the other types; 20 Linked principals in three columns.
    for number, key, name in (('19', 'claimSource', 'claim-source'), ('20', 'repairer', 'repairer'), ('22', 'storage', 'storage'), ('23', 'tpe', 'tpe')):
        shoot(number, name, edit(key))
        check(f'{name}: no tabs', page.locator('nav.tabs').count() == 0)
    load(edit('repairer'))
    page.set_viewport_size({'width': 1580, 'height': 1000})
    load(edit('repairer'))
    columns = page.evaluate("() => getComputedStyle(document.querySelector('.contact-links')).gridTemplateColumns.split(' ').length")
    check(f'repairer: Linked principals in three columns ({columns})', columns == 3)

    # O: Save contact returns to the Contacts list.
    page.click('.contact-editor button[type=submit]')
    page.wait_for_load_state('networkidle')
    check(f'save contact: back on the list ({page.url})', page.url.rstrip('/').endswith('/Administration/Contacts'))

    check(f'no page errors {errors}', not errors)
    browser.close()

result = {'date': '2026-10-13', 'ok': ok[0], 'fail': fails}
(out / 'verification.json').write_text(json.dumps(result, indent=2) + '\n')
print('RESULT', json.dumps(result))
