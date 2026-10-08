"""Self-check and screenshot capture for the v35 Case aside proposals.

    python check-rail-v35.py            checks, then screenshots
    python check-rail-v35.py --no-shots checks only

Loads the mockup in headless Chromium (Playwright) and asserts, for live and
each of designs 1 to 6 in every Case state: no script error; every report
blocker present in page order with its requirement, source, reason and what
clears it, and no count of them (FRD-13); in 1 to 6 every outstanding Case
requirement listed with its source (item J) and Original report missing
linking to Files (item K); a step whose control says what the step says is
that control once; every visible word is the application's own or a
fixture's; nothing spills sideways at 1580, 1440 and 760; the sticky aside
fits the viewport at 1580. It then writes the round-3 screenshots and
verification.json. This is evidence about the mockup, not the application.

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
DESIGNS = ['live', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12']
PROPOSALS = DESIGNS[1:]
ROUND4 = ['7', '8', '9', '10', '11', '12']
STATES = ['notready', 'review', 'engineer', 'near', 'ready', 'stale', 'audit']
WIDTHS = [(1580, 1000), (1440, 900), (760, 1000)]
# Every word the aside may show besides the fixtures' text: the live labels
# the aside already renders (CaseWorkspaceLabels, OperatorLabels).
LABELS = {'Figures', 'Next action', 'Repair cost inc VAT', "Engineer's Value", 'Repair cost of value', 'Report not ready',
          'Outstanding requirements', 'Assign Engineer', 'Generate report', 'Create audit', 'Report', 'AI',
          'Estimate draft ready', 'Review estimate', 'Cancellation received', 'Open message', 'Source:', 'Why:',
          'Staff accounts & roles', 'A newer fact changed after this generation. Generate again before delivery.', '—',
          'Case details', 'Claim', 'Inspection details', 'Vehicle', 'Damage', 'Valuation', 'Repair Spec', 'Decisions',
          'Files', 'Notes', 'Next action'}

ok = []
fail = []


def check(name, condition, detail=''):
    (ok if condition else fail).append(name if condition else f'{name} {detail}'.strip())


def url(design, state, extra=''):
    return PAGE.as_uri() + f'?design={design}&state={state}&strip=0' + extra


def load(page, design, state, extra=''):
    page.goto(url(design, state, extra))
    page.wait_for_function(f"document.documentElement.dataset.ready === '{design}:{state}'")


READ = """() => {
    const aside = document.querySelector('aside[data-case-aside]');
    const scope = [aside, ...document.querySelectorAll('[data-rail-elsewhere]')];
    const all = (sel) => scope.flatMap(e => [...e.querySelectorAll(sel)]);
    const rect = aside.getBoundingClientRect();
    const text = (e) => e.textContent.replace(/\\s+/g, ' ').trim();
    const words = [];
    for (const root of scope) {
      const walk = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
      while (walk.nextNode()) { const t = walk.currentNode.textContent.replace(/\\s+/g, ' ').trim(); if (t) words.push(t); }
    }
    const next = document.querySelector('[data-next-action]');
    const nr = next ? next.getBoundingClientRect() : null;
    const inNext = next ? [...next.querySelectorAll('[data-report-blocker], [data-case-requirement]')].filter(e => !e.closest('.rail-bar-more')).length
      + (next.matches('[data-case-requirement]') ? 1 : 0) : 0;
    return { errors: window.mockupErrors, words, text: scope.map(text).join(' '),
      blockers: all('[data-report-blocker]').map(text),
      requirements: all('[data-case-requirement]').map(text),
      nextText: next ? text(next) : '', inNext, nextInAside: !!aside.querySelector('[data-next-action]'),
      overflowX: rect.right > window.innerWidth + 1 || scope.flatMap(e => [e, ...e.querySelectorAll('*')]).some(e => {
        const s = getComputedStyle(e); return e.scrollWidth > e.clientWidth + 1 && s.overflowX !== 'visible' && s.overflowX !== 'hidden'; }),
      bottom: rect.bottom, nextTop: nr ? nr.top : null, nextBottom: nr ? nr.bottom : null,
      figures: !!aside.querySelector('[data-figures]') };
}"""


def main():
    shots = '--no-shots' not in sys.argv
    SHOTS.mkdir(exist_ok=True)
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=os.environ.get('PEGASUS_CHROME') or None)
        page = browser.new_page(viewport={'width': 1580, 'height': 1000})
        console = []
        page.on('console', lambda m: console.append(m.text) if m.type == 'error' else None)
        page.on('pageerror', lambda e: console.append(str(e)))
        load(page, 'live', 'notready')
        presets = page.evaluate('window.v35.PRESETS')

        for design in DESIGNS:
            for state in STATES:
                tag = f'{design}/{state}'
                preset = presets[state]
                blockers = preset['blockers']
                reqs = preset.get('requirements') or []
                for width, height in WIDTHS:
                    page.set_viewport_size({'width': width, 'height': height})
                    load(page, design, state)
                    r = page.evaluate(READ)
                    at = f'{tag}@{width}'
                    check(f'{at} no script error', not r['errors'], r['errors'])
                    check(f'{at} Figures', r['figures'])
                    check(f'{at} no sideways spill', not r['overflowX'])
                    if width != 1580:
                        continue
                    check(f'{tag} one row per blocker', len(r['blockers']) == len(blockers), f"{len(r['blockers'])} != {len(blockers)}")
                    # Page order in the rail; 9 puts each blocker in its own section,
                    # where the order is the page's by construction.
                    pairs = zip(r['blockers'], blockers) if design != '9' else [
                        (next((row for row in r['blockers'] if b['requirement'] in row and b['how'] in row), ''), b) for b in blockers]
                    for row, b in pairs:
                        for key in ['requirement', 'source', 'why', 'how']:
                            check(f"{tag} {b['requirement']} shows its {key}", b[key] in row, row[:80])
                    if design != 'live' and reqs:
                        check(f'{tag} every Case requirement', len(r['requirements']) == len(reqs), r['requirements'])
                        for row, q in zip(r['requirements'], reqs):
                            check(f"{tag} {q['title']} with its source", q['title'] in row and q['source'] in row, row)
                    if design == 'live' and reqs:
                        check(f'{tag} today names the first requirement only', reqs[0]['title'] in r['text'] and reqs[1]['title'] not in r['text'])
                    check(f'{tag} no count', not re.search(r'\b\d+\s+(items?|blockers?|outstanding|more)\b', r['text'], re.I))
                    allowed = set(LABELS)
                    for b in blockers:
                        allowed |= {b[k] for k in ['requirement', 'source', 'why', 'how']}
                    for q in reqs:
                        allowed |= {q[k] for k in ['title', 'source', 'why'] if q[k]}
                    stray = [part for w in r['words'] for part in w.split(' · ') if part and part not in allowed and not re.fullmatch(r'£[\d,]+\.\d\d|\d+%', part)]
                    check(f'{tag} only application words', not stray, stray)
                    s = preset['step']
                    if s and design != 'live' and s['label'] == s['control']:
                        check(f'{tag} step said once', r['nextText'].count(s['label']) == 1, r['nextText'])
                    check(f'{tag} sticky aside fits at 1580', r['bottom'] <= 1000 + 1, r['bottom'])
                    if design in ROUND4:
                        check(f'{tag} Next action holds one item', r['inNext'] <= 1, r['inNext'])
                    if design == '3' and r['nextBottom'] is not None:
                        check(f'{tag} Next action in view', 0 <= r['nextTop'] and r['nextBottom'] <= 1000, r['nextBottom'])

        page.set_viewport_size({'width': 1580, 'height': 1000})
        for design in PROPOSALS:
            load(page, design, 'notready')
            check(f'{design} Original report missing opens Files (K)', page.evaluate("""[...document.querySelectorAll('[data-case-requirement]')]
                .filter(e => e.textContent.includes('Original report missing'))
                .some(e => (e.closest('.rail-bar-row') ?? e).querySelector('[data-section-jump="files"]') || e.matches('[data-section-jump="files"]'))"""))
            check(f'{design} Overview image opens Files on Images', page.locator('[data-report-blocker="files"] [data-section-tab="images"]').count() == 1)
            load(page, design, 'near')
            check(f'{design} VAT blocker claims Repair Spec', page.locator('[data-edit-focus="#estimate-vat-status"]').count() == 1)
            check(f'{design} Accounts for an Administrator', page.locator('[data-blocker-accounts]').count() >= 1)
            load(page, design, 'near', '&role=user')
            check(f'{design} no Accounts link for a User', page.locator('[data-blocker-accounts]').count() == 0)
            load(page, design, 'review', '&tone=primary')
            check(f'{design} primary step', page.locator('[data-next-step].btn--primary').count() == 1)

        # 4 and 6: one row per section naming what it is missing.
        for design in ['4', '6']:
            load(page, design, 'notready')
            sects = page.evaluate("[...document.querySelectorAll('aside .rail-sect')].map(d => [d.dataset.railGroup, d.querySelector('summary small').textContent])")
            keys = []
            for b in presets['notready']['blockers']:
                if not keys or keys[-1] != b['section']:
                    keys.append(b['section'])
            check(f'{design} one row per section', [k for k, _ in sects] == keys, sects)
            for key, names in sects:
                for b in [b for b in presets['notready']['blockers'] if b['section'] == key]:
                    check(f"{design} {key} row names {b['requirement']}", b['requirement'] in names)
        # 6: the section row marks, and the aside follows the page.
        load(page, '6', 'notready')
        marks = page.evaluate("[...document.querySelectorAll('[data-section-link]')].filter(a => a.querySelector('.rail-nav-mark')).map(a => a.dataset.sectionLink)")
        check('6 section row marks', marks == ['overview', 'claim', 'inspection', 'vehicle', 'estimate', 'settlement', 'files'], marks)
        check('6 Case details open at the top', page.evaluate("document.querySelector('aside .rail-sect.is-here')?.dataset.railGroup") == 'overview')
        page.evaluate("document.querySelector('#section-valuation').scrollIntoView({block: 'start', behavior: 'instant'}); window.v35.follow()")
        check('6 Valuation open when in view', page.evaluate("[...document.querySelectorAll('aside .rail-sect[open]')].map(d => d.dataset.railGroup)") == ['valuation'])
        # 5: the first thing in full.
        load(page, '5', 'notready')
        check('5 first requirement in full', 'Original report missing' in page.locator('aside .rail-first').text_content())
        load(page, '5', 'engineer')
        check('5 first blocker in full', 'Pre-incident condition' in page.locator('aside .rail-first').text_content())
        # Round 4: each design's place for the rest.
        load(page, '7', 'notready')
        check('7 dialog closed at first', page.locator('[data-rail-dialog]').is_hidden())
        page.click('[data-rail-dialog-open]')
        check('7 dialog opens', page.locator('[data-rail-dialog]').is_visible())
        page.click('[data-rail-dialog] a[data-section-jump="vehicle"] >> nth=0')
        check('7 a link lands and closes the dialog', page.locator('[data-rail-dialog]').is_hidden())
        load(page, '8', 'notready')
        check('8 card closed at first', page.evaluate("document.querySelector('[data-rail-fold]').open") is False)
        load(page, '9', 'notready')
        check('9 field marks', page.locator('.rail-field-mark').count() == 20, page.locator('.rail-field-mark').count())
        check('9 section notes', page.locator('.rail-here').count() == 9, page.locator('.rail-here').count())
        check('9 no list in the rail', page.locator('aside [data-report-blocker]').count() == 0)
        load(page, '10', 'notready')
        check('10 list at the top of Report', page.locator('#section-report .rail-report [data-report-blocker]').count() == 22)
        check('10 Next action links to Report', page.locator('aside a.rail-open[href="#section-report"]').count() == 1)
        load(page, '11', 'notready')
        done = page.evaluate("[...document.querySelectorAll('.rail-check--done')].map(e => e.dataset.railGroup)")
        check('11 every section listed', page.locator('[data-rail-checklist] [data-rail-group]').count() == 10)
        check('11 Report done', done == ['report'], done)
        load(page, '12', 'notready')
        check('12 no Next action in the aside', page.locator('aside [data-next-action]').count() == 0)
        check('12 bar above the sections', page.evaluate("document.querySelector('.rail-bar').nextElementSibling.classList.contains('workspace')"))
        check('no console error', not console, console)

        if shots:
            n = 0
            prefix = ['r3']

            def shot(name, design, state, width=1580, height=1000, extra='', before=None):
                nonlocal n
                n += 1
                page.set_viewport_size({'width': width, 'height': height})
                load(page, design, state, extra)
                if before:
                    page.evaluate(before)
                path = SHOTS / f'{prefix[0]}-{n:02d}-{name}.png'
                page.screenshot(path=str(path))
                return path.name

            names = []
            if '--r3' in sys.argv:
                for d in DESIGNS[:7]:
                    names.append(shot(f'{d}-notready-1580', d, 'notready'))
                names.append(shot('2-notready-open-1580', '2', 'notready', before="document.querySelectorAll('aside .rail-line').forEach((d,i)=>d.open=i<3)"))
                names.append(shot('4-notready-open-1580', '4', 'notready', before="document.querySelector('aside .rail-sect[data-rail-group=\"vehicle\"]').open=true"))
                names.append(shot('6-notready-valuation-1580', '6', 'notready', before="document.querySelector('#section-valuation').scrollIntoView({block:'start', behavior:'instant'}); window.v35.follow()"))
                for d in DESIGNS[:7]:
                    names.append(shot(f'{d}-review-1580', d, 'review'))
                for d in DESIGNS[:7]:
                    names.append(shot(f'{d}-near-1580', d, 'near'))
                for d in DESIGNS[:7]:
                    names.append(shot(f'{d}-audit-1580', d, 'audit'))
                for d in DESIGNS[:7]:
                    names.append(shot(f'{d}-notready-1440', d, 'notready', 1440, 900))
                for d in DESIGNS[:7]:
                    names.append(shot(f'{d}-notready-760', d, 'notready', 760, 1000))
            else:
                prefix[0] = 'r4'
                four = ['live'] + ROUND4
                for d in four:
                    names.append(shot(f'{d}-notready-1580', d, 'notready'))
                names.append(shot('7-notready-dialog-1580', '7', 'notready', before="document.querySelector('[data-rail-dialog-open]').click()"))
                names.append(shot('8-notready-open-1580', '8', 'notready', before="document.querySelector('[data-rail-fold]').open=true"))
                names.append(shot('9-notready-vehicle-1580', '9', 'notready', before="document.querySelector('#section-vehicle').scrollIntoView({block:'start', behavior:'instant'})"))
                names.append(shot('10-notready-report-1580', '10', 'notready', before="document.querySelector('#section-report').scrollIntoView({block:'start', behavior:'instant'})"))
                names.append(shot('11-notready-open-1580', '11', 'notready', before="document.querySelector('[data-rail-checklist] .rail-sect[data-rail-group=vehicle]').open=true"))
                names.append(shot('12-notready-open-1580', '12', 'notready', before="document.querySelector('[data-rail-bar-more]').open=true"))
                for st in ['review', 'engineer', 'near']:
                    for d in four:
                        names.append(shot(f'{d}-{st}-1580', d, st))
                for d in ROUND4:
                    names.append(shot(f'{d}-notready-1440', d, 'notready', 1440, 900))
                for d in ROUND4:
                    names.append(shot(f'{d}-notready-760', d, 'notready', 760, 1000))
            print('shots', len(names))
        browser.close()

    result = {'date': date.today().isoformat(), 'fail': fail, 'okCount': len(ok)}
    (CURRENT / 'verification.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf8')
    print('RESULT', json.dumps({'fail': fail, 'okCount': len(ok)}))
    return 1 if fail else 0


if __name__ == '__main__':
    sys.exit(main())
