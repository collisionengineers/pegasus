"""Self-check and screenshot capture for the v35 Case aside proposals.

    python check-rail-v35.py            checks, then screenshots
    python check-rail-v35.py --no-shots checks only

Loads the mockup in headless Chromium (Playwright) and asserts, for live and
for each of A, B and C in every Case state: no script error; every report
blocker present in page order with its requirement, source, reason and what
clears it (FRD-13), and no count of them; in A to C a step whose control
says what the step says is that control once, not the words twice; every
visible word is the application's own or a fixture's; nothing spills
sideways at 1580, 1440 and 760; at 1580 the sticky aside fits the viewport,
and in C the step stays in view however long the list. It then writes the
screenshots and verification.json. This is evidence about the mockup, not
the application.

Set PEGASUS_CHROME to a Chromium executable when Playwright's own browser
does not match the installed driver.
"""
import json
import os
import re
import sys
from datetime import date
from pathlib import Path

from playwright.sync_api import sync_playwright

CURRENT = Path(__file__).resolve().parent
SHOTS = CURRENT / 'v35-shots'
PAGE = CURRENT / 'pegasus_case_rail_v35.html'
DESIGNS = ['live', 'a', 'b', 'c']
STATES = ['review', 'engineer', 'near', 'ready', 'stale', 'audit']
WIDTHS = [(1580, 1000), (1440, 900), (760, 1000)]
# Every word the aside may show besides the fixtures' blocker text: the live
# labels the aside already renders (CaseWorkspaceLabels, OperatorLabels).
LABELS = {'Figures', 'Next action', 'Repair cost inc VAT', "Engineer's Value", 'Repair cost of value', 'Report not ready',
          'Assign Engineer', 'Generate report', 'Create audit', 'Report', 'AI', 'Estimate draft ready', 'Review estimate',
          'Cancellation received', 'Open message', 'Source:', 'Why:', 'Staff accounts & roles',
          'A newer fact changed after this generation. Generate again before delivery.', '—', '·',
          'Case details', 'Claim', 'Inspection details', 'Vehicle', 'Damage', 'Valuation', 'Repair Spec', 'Decisions',
          'Files', 'Notes'}

ok = []
fail = []


def check(name, condition, detail=''):
    (ok if condition else fail).append(name if condition else f'{name} {detail}'.strip())


def url(design, state, extra=''):
    return PAGE.as_uri() + f'?design={design}&state={state}&strip=0' + extra


def load(page, design, state, extra=''):
    page.goto(url(design, state, extra))
    page.wait_for_function(f"document.documentElement.dataset.ready === '{design}:{state}'")


def main():
    shots = '--no-shots' not in sys.argv
    SHOTS.mkdir(exist_ok=True)
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=os.environ.get('PEGASUS_CHROME') or None)
        page = browser.new_page(viewport={'width': 1580, 'height': 1000})
        console = []
        page.on('console', lambda m: console.append(m.text) if m.type == 'error' else None)
        page.on('pageerror', lambda e: console.append(str(e)))
        load(page, 'live', 'review')
        presets = page.evaluate('window.v35.PRESETS')
        page.evaluate("document.querySelectorAll('.rail-b-row').forEach(d => d.open = true)")

        for design in DESIGNS:
            for state in STATES:
                tag = f'{design}/{state}'
                preset = presets[state]
                for width, height in WIDTHS:
                    page.set_viewport_size({'width': width, 'height': height})
                    load(page, design, state)
                    r = page.evaluate("""() => {
                        const aside = document.querySelector('aside[data-case-aside]');
                        const rect = aside.getBoundingClientRect();
                        const rows = [...aside.querySelectorAll('[data-report-blocker]')].map(li => li.textContent.replace(/\\s+/g, ' ').trim());
                        const words = [];
                        const walk = document.createTreeWalker(aside, NodeFilter.SHOW_TEXT);
                        while (walk.nextNode()) { const t = walk.currentNode.textContent.replace(/\\s+/g, ' ').trim(); if (t) words.push(t); }
                        const next = aside.querySelector('[data-next-action]');
                        const step = aside.querySelector('[data-next-step]');
                        const sr = step ? step.getBoundingClientRect() : null;
                        return { errors: window.mockupErrors, rows, words, text: aside.textContent.replace(/\\s+/g, ' '),
                          nextText: next ? next.textContent.replace(/\\s+/g, ' ') : '',
                          overflowX: [aside, ...aside.querySelectorAll('*')].some(e => e.scrollWidth > e.clientWidth + 1 && getComputedStyle(e).overflowX !== 'visible' && getComputedStyle(e).overflowX !== 'hidden') || rect.right > window.innerWidth + 1,
                          bottom: rect.bottom, stepBottom: sr ? sr.bottom : null, stepTop: sr ? sr.top : null,
                          figures: !!aside.querySelector('[data-figures]') };
                    }""")
                    at = f'{tag}@{width}'
                    check(f'{at} no script error', not r['errors'], r['errors'])
                    check(f'{at} Figures', r['figures'])
                    check(f'{at} no sideways spill', not r['overflowX'])
                    if width != 1580:
                        continue
                    blockers = preset['blockers']
                    check(f'{tag} one row per blocker', len(r['rows']) == len(blockers), f"{len(r['rows'])} != {len(blockers)}")
                    for row, b in zip(r['rows'], blockers):
                        for key in ['requirement', 'source', 'why', 'how']:
                            check(f"{tag} {b['requirement']} shows its {key}", b[key] in row, row[:80])
                    check(f'{tag} no count of blockers', not re.search(r'\b\d+\s+(items?|blockers?|outstanding|more)\b', r['text'], re.I))
                    allowed = LABELS | {b[k] for b in blockers for k in ['requirement', 'source', 'why', 'how']}
                    allowed |= {f"{b['source']} · {b['why']}" for b in blockers}
                    stray = [w for w in r['words'] if w not in allowed and not re.fullmatch(r'£[\d,]+\.\d\d|\d+%', w)]
                    check(f'{tag} only application words', not stray, stray)
                    s = preset['step']
                    if s:
                        if design != 'live' and s['label'] == s['control']:
                            check(f"{tag} step said once", r['nextText'].count(s['label']) == 1, r['nextText'])
                        check(f"{tag} step present", s['label'] in r['text'])
                    check(f'{tag} sticky aside fits at 1580', r['bottom'] <= 1000 + 1, r['bottom'])
                    if design == 'c' and r['stepBottom'] is not None:
                        check(f'{tag} step in view', 0 <= r['stepTop'] and r['stepBottom'] <= 1000, r['stepBottom'])

        # A blocker's control lands on its section; Files opens on Images.
        page.set_viewport_size({'width': 1580, 'height': 1000})
        for design in ['a', 'b', 'c']:
            load(page, design, 'review')
            link = page.locator('aside [data-report-blocker="files"] a[data-section-tab="images"]')
            check(f'{design} Overview image opens Files on Images', link.count() == 1)
            load(page, design, 'near')
            check(f'{design} VAT blocker claims Repair Spec', page.locator('aside [data-edit-focus="#estimate-vat-status"]').count() == 1)
            check(f'{design} Accounts for an Administrator', page.locator('aside [data-blocker-accounts]').count() == 1)
            load(page, design, 'near', '&role=user')
            check(f'{design} no Accounts link for a User', page.locator('aside [data-blocker-accounts]').count() == 0)
            load(page, design, 'review', '&tone=primary')
            check(f'{design} primary step', page.locator('aside [data-next-step].btn--primary').count() == 1)
        check('no console error', not console, console)

        if shots:
            n = 0

            def shot(name, design, state, width=1580, height=1000, extra='', aside=False, scroll=0, before=None):
                nonlocal n
                n += 1
                page.set_viewport_size({'width': width, 'height': height})
                load(page, design, state, extra)
                if before:
                    page.evaluate(before)
                if scroll:
                    page.evaluate(f'window.scrollTo(0, {scroll})')
                path = SHOTS / f'{n:02d}-{name}.png'
                if aside:
                    page.locator('aside[data-case-aside]').screenshot(path=str(path))
                else:
                    page.screenshot(path=str(path))
                return path.name

            names = []
            for d in DESIGNS:
                names.append(shot(f'{d}-review-1580', d, 'review'))
            for d in DESIGNS:
                names.append(shot(f'{d}-engineer-1580', d, 'engineer'))
            for d in DESIGNS:
                names.append(shot(f'{d}-near-1580', d, 'near'))
            names.append(shot('b-near-open-1580', 'b', 'near', before="document.querySelectorAll('.rail-b-row').forEach((d,i)=>d.open=i<2)"))
            for d in DESIGNS:
                names.append(shot(f'{d}-stale-1580', d, 'stale'))
            for d in DESIGNS:
                names.append(shot(f'{d}-audit-1580', d, 'audit'))
            for d in DESIGNS:
                names.append(shot(f'{d}-review-1440', d, 'review', 1440, 900))
            for d in DESIGNS:
                names.append(shot(f'{d}-review-760', d, 'review', 760, 1000))
            for d in ['a', 'b', 'c']:
                names.append(shot(f'{d}-review-primary-1580', d, 'review', extra='&tone=primary'))
            print('shots', len(names))
        browser.close()

    result = {'date': date.today().isoformat(), 'fail': fail, 'okCount': len(ok)}
    (CURRENT / 'verification.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf8')
    print('RESULT', json.dumps({'fail': fail, 'okCount': len(ok)}))
    return 1 if fail else 0


if __name__ == '__main__':
    sys.exit(main())
