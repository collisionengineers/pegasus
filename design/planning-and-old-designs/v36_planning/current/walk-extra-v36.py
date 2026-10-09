"""Extra live-walk passes for v36: Files tabs, the viewer and crop, the
Work Centre and Cases list buttons, and the edit-session mechanics.

    python walk-extra-v36.py <caseId> [--base https://localhost:5240]

Writes live-shots/x-*.png and captured/extra.json. Evidence about the
running synthetic host only.
"""
import json
import os
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

CURRENT = Path(__file__).resolve().parent
SHOTS = CURRENT / 'live-shots'
CAPTURED = CURRENT / 'captured'


def main():
    args = sys.argv[1:]
    base = args[args.index('--base') + 1] if '--base' in args else 'https://localhost:5240'
    case_id = next(a for a in args if not a.startswith('--') and a != base)
    out = {'errors': []}
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=os.environ.get('PEGASUS_CHROME') or None)
        context = browser.new_context(viewport={'width': 1580, 'height': 1000}, ignore_https_errors=True)
        page = context.new_page()
        page.on('pageerror', lambda e: out['errors'].append(str(e)))
        shot = lambda name: page.screenshot(path=str(SHOTS / f'x-{name}.png'))

        # Work Centre and the Cases list (busy-state buttons).
        page.goto(f'{base}/'); page.wait_for_load_state('networkidle'); page.wait_for_timeout(600)
        shot('work-centre-1580')
        out['workCentreButtons'] = page.evaluate("[...document.querySelectorAll('a.btn, button.btn')].filter(e => e.offsetParent).map(e => ({tag: e.tagName, t: e.textContent.replace(/\\s+/g,' ').trim(), cls: e.className, busy: e.hasAttribute('data-busy-label')}))")
        page.goto(f'{base}/Cases'); page.wait_for_load_state('networkidle'); page.wait_for_timeout(600)
        shot('cases-list-1580')
        first = page.query_selector('tbody tr')
        if first:
            first.click(); page.wait_for_timeout(600); shot('cases-list-quick-detail-1580')
        out['casesButtons'] = page.evaluate("[...document.querySelectorAll('a.btn, button.btn')].filter(e => e.offsetParent).map(e => ({tag: e.tagName, t: e.textContent.replace(/\\s+/g,' ').trim(), cls: e.className, busy: e.hasAttribute('data-busy-label')}))")

        # Files: Images tab, read.
        page.goto(f'{base}/Cases/{case_id}?section=files'); page.wait_for_load_state('networkidle'); page.wait_for_timeout(900)
        page.click('#section-files .tab:has-text("Images")'); page.wait_for_timeout(400)
        page.evaluate("document.querySelector('#section-files').scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(300)
        shot('files-images-read-1580')
        tile = page.query_selector('#section-files [data-evidence-item]')
        if tile:
            tile.click(); page.wait_for_timeout(800); shot('viewer-read-1580')
            page.keyboard.press('Escape'); page.wait_for_timeout(300)
        # Notes section read.
        page.evaluate("document.querySelector('#section-notes').scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(300)
        shot('notes-read-1580')

        # Edit session: enter from a section head (Vehicle) and watch what moves.
        page.goto(f'{base}/Cases/{case_id}?section=vehicle'); page.wait_for_load_state('networkidle'); page.wait_for_timeout(900)
        before = page.evaluate("({y: window.scrollY, top: document.querySelector('#section-vehicle').getBoundingClientRect().top, h: document.querySelector('#section-vehicle').getBoundingClientRect().height})")
        page.click('#section-vehicle [data-section-edit]')
        page.wait_for_function("document.querySelector('[data-case-record]')?.classList.contains('is-editing')"); page.wait_for_timeout(900)
        after = page.evaluate("({y: window.scrollY, top: document.querySelector('#section-vehicle').getBoundingClientRect().top, h: document.querySelector('#section-vehicle').getBoundingClientRect().height})")
        out['sectionEditEntry'] = {'before': before, 'after': after}
        shot('vehicle-after-section-edit-1580')
        # Type a value, leave the field, watch the status word and what redraws.
        page.fill('#section-vehicle input[name="vehicleMake"], #section-vehicle input[id*="make" i]', 'Ford')
        page.keyboard.press('Tab')
        page.wait_for_timeout(150)
        out['statusWhileSaving'] = page.evaluate("document.querySelector('[data-lease-line]')?.textContent.trim()")
        shot('vehicle-saving-1580')
        page.wait_for_timeout(2500)
        out['statusAfterSave'] = page.evaluate("document.querySelector('[data-lease-line]')?.textContent.trim()")
        out['focusAfterSave'] = page.evaluate("document.activeElement ? (document.activeElement.id || document.activeElement.name || document.activeElement.tagName) : null")
        out['scrollAfterSave'] = page.evaluate("window.scrollY")
        shot('vehicle-saved-1580')
        # Files in edit: images tab with tile tools; the viewer with crop.
        page.evaluate("document.querySelector('#section-files')?.scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(900)
        page.click('#section-files .tab:has-text("Images")'); page.wait_for_timeout(400)
        page.evaluate("document.querySelector('#section-files').scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(300)
        shot('files-images-edit-1580')
        picker = page.query_selector('#section-files .tag-picker > summary')
        if picker:
            picker.click(); page.wait_for_timeout(300); shot('files-tag-picker-edit-1580'); page.keyboard.press('Escape')
        tile = page.query_selector('#section-files [data-evidence-item]')
        if tile:
            tile.click(); page.wait_for_timeout(800); shot('viewer-edit-1580')
            crop = page.query_selector('.case-viewer [data-viewer-crop], .case-viewer button:has-text("Crop")')
            if crop:
                crop.click(); page.wait_for_timeout(400); shot('viewer-crop-edit-1580')
            page.keyboard.press('Escape'); page.wait_for_timeout(200); page.keyboard.press('Escape'); page.wait_for_timeout(300)
        # Repair Spec: New repair spec via More, then the editor; full screen.
        page.evaluate("document.querySelector('#section-estimate').scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(300)
        more = page.query_selector('#section-estimate details.menu > summary')
        if more:
            more.click(); page.wait_for_timeout(300); shot('estimate-more-edit-1580')
            new = page.query_selector('#section-estimate .menu-body button:has-text("New repair spec"), #section-estimate .menu-body a:has-text("New repair spec")')
            if new:
                new.click(); page.wait_for_timeout(1500)
                page.evaluate("document.querySelector('#section-estimate').scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(300)
                shot('estimate-new-edit-1580')
                page.screenshot(path=str(SHOTS / 'x-estimate-new-edit-full.png'), full_page=True)
                expand = page.query_selector('#section-estimate [data-estimate-expand]')
                if expand:
                    expand.click(); page.wait_for_timeout(500); shot('estimate-fullscreen-edit-1580'); page.keyboard.press('Escape'); page.wait_for_timeout(300)
        # Decisions: choose an outcome to see the radios with a value, then the Report fee tab.
        page.evaluate("document.querySelector('#section-settlement').scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(300)
        rep = page.query_selector('#section-settlement [data-decision-radios] [data-radio-value="repairable"]')
        if rep:
            rep.click(); page.wait_for_timeout(2000); shot('decisions-repairable-edit-1580')
        page.evaluate("document.querySelector('#section-report').scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(300)
        fee = page.query_selector('#section-report .report-tab:has-text("Fee"), #section-report [role=tab]:has-text("Fee")')
        if fee:
            fee.click(); page.wait_for_timeout(400); shot('report-fee-edit-1580')
        # Done, then read-mode follow-ups: Decisions with a value, Files images.
        page.click('.ribbon-actions button[form="case-finish-editing-form"]')
        page.wait_for_function("!document.querySelector('[data-case-record]')?.classList.contains('is-editing')"); page.wait_for_timeout(800)
        page.evaluate("document.querySelector('#section-settlement').scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(300)
        shot('decisions-repairable-read-1580')
        page.evaluate("document.querySelector('#section-vehicle').scrollIntoView({block: 'start', behavior: 'instant'})"); page.wait_for_timeout(300)
        shot('vehicle-read-after-1580')
        browser.close()
    (CAPTURED / 'extra.json').write_text(json.dumps(out, indent=1), encoding='utf8')
    print(json.dumps(out, indent=1)[:3000])


if __name__ == '__main__':
    main()
