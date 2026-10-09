"""Self-check and screenshot capture for the v36 Case page walk mockup.

    python check-case-walk-v36.py            checks, then screenshots
    python check-case-walk-v36.py --no-shots checks only

Loads the mockup in headless Chromium (Playwright) and asserts, for today
and the proposal, in every Case state the frames hold, read and edit where
the state has an edit frame, Scroll and Tabs, at 1580, 1440 and 760: no
script error; the frame rendered; the section row is not clipped in the
proposal (it is today at 1440 on the Audit Case and at 760 everywhere);
nothing spills sideways; the sticky aside fits the viewport at 1580; read
mode shows no live control outside the Notes composer; every finding's
switch changes only regions it marks; each dialog opens by name; the
viewer opens; the proposal's valuation card boxes are 32px; the Decisions
"Not recorded" segment is gone under the box variant. Then it writes the
numbered screenshots and verification.json. This is evidence about the
mockup, not the application.

Set PEGASUS_CHROME to a Chromium executable when Playwright's own browser
does not match the installed driver.
"""
import json
import os
import sys
from datetime import date
from pathlib import Path

from playwright.sync_api import sync_playwright

CURRENT = Path(__file__).resolve().parent
SHOTS = CURRENT / 'v36-shots'
PAGE = CURRENT / 'pegasus_case_walk_v36.html'
STATES = {'engineer': ['read', 'edit'], 'review': ['read'], 'held': ['read'], 'colleague': ['read'], 'notready': ['read'], 'views': ['read', 'edit'], 'viewsinsp': ['read']}
WIDTHS = [(1580, 1000), (1440, 900), (760, 1000)]
DIALOGS = ['case-hold-dialog', 'case-close-dialog', 'case-correct-principal-dialog', 'case-return-review-dialog']

ok, fail = [], []


def check(name, condition, detail=''):
    (ok if condition else fail).append(name if condition else f'{name} {detail}'.strip())


def url(q):
    return PAGE.as_uri() + '?' + q + '&strip=0'


READ = """() => {
  const record = document.querySelector('[data-case-record]');
  const nav = document.querySelector('.section-nav');
  const aside = document.querySelector('aside.workspace-aside');
  const text = (e) => e ? e.textContent.replace(/\\s+/g, ' ').trim() : '';
  const navClipped = nav ? (nav.scrollWidth - nav.clientWidth) : 0;
  const lastLink = nav ? [...nav.querySelectorAll('.section-link')].pop() : null;
  const lastVisible = lastLink ? lastLink.getBoundingClientRect().right <= nav.getBoundingClientRect().right + 1 : true;
  const spill = [...document.querySelectorAll('.case-record, .case-record *')].some(e => {
    const s = getComputedStyle(e); return e.tagName !== 'INPUT' && e.scrollWidth > e.clientWidth + 1 && s.overflowX !== 'visible' && s.overflowX !== 'hidden' && !e.closest('.section-nav') && !e.closest('table') && !e.closest('.thumbs'); });
  const addressClips = (() => { const i = document.querySelector('#f-claimant-address'); return i ? i.scrollWidth > i.clientWidth + 1 : null; })();
  const liveControls = record && !record.classList.contains('is-editing')
    ? [...record.querySelectorAll('#case-main input:not([type=hidden]):not([readonly]), #case-main select, #case-main textarea')].filter(e => e.offsetParent && !e.closest('.note-form') && !e.closest('.v36-f28') && !e.closest('[data-estimate-import]') && e.type !== 'file').map(e => e.name || e.id || e.className)
    : [];
  const cardBoxes = [...document.querySelectorAll('.valuation-card-fig .fi, .valuation-card-fig input')].filter(e => e.offsetParent).map(e => Math.round(e.getBoundingClientRect().height));
  return {
    errors: window.mockupErrors, ready: document.documentElement.dataset.ready, applied: window.v36Applied || [],
    editing: !!record && record.classList.contains('is-editing'),
    navClipped, lastVisible, spill, liveControls, addressClips,
    asideBottom: aside ? aside.getBoundingClientRect().bottom : null,
    asideStatic: aside ? getComputedStyle(aside).position : null,
    changed: [...document.querySelectorAll('[data-v36-change]')].map(e => e.dataset.v36Change).filter((v, i, a) => a.indexOf(v) === i),
    cardBoxes,
    notRecordedSegments: [...document.querySelectorAll('.decisions .pick-radio')].filter(e => text(e) === 'Not recorded').length,
    dialogsOpen: [...document.querySelectorAll('[data-dialog]:not([hidden])')].map(d => d.dataset.dialog),
    viewerOpen: !!document.querySelector('.case-viewer:not([hidden])'),
    stickyH: record ? getComputedStyle(record).getPropertyValue('--sticky-h') : null,
    tabsActive: [...document.querySelectorAll('.record-section.is-active')].map(s => s.dataset.section),
    guid: /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i.test(text(document.querySelector('#section-estimate'))),
  };
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

        def load(q, width=1580, height=1000):
            page.set_viewport_size({'width': width, 'height': height})
            page.goto(url(q))
            page.wait_for_function("document.documentElement.dataset.ready")
            page.wait_for_timeout(250)
            return page.evaluate(READ)

        load('design=today&state=engineer')
        findings = page.evaluate('window.v36.findings.map(f => ({id: f.id, tier: f.tier, surface: f.surface, title: f.title, variable: f.variable || null, options: f.options || null, states: f.states || null}))')

        for design in ['today', 'proposal']:
            for state, modes in STATES.items():
                for mode in modes:
                    for layout in ['scroll', 'tabs']:
                        for width, height in WIDTHS:
                            tag = f'{design}/{state}/{mode}/{layout}@{width}'
                            r = load(f'design={design}&state={state}&mode={mode}&layout={layout}' + ('&section=vehicle' if layout == 'tabs' else ''), width, height)
                            check(f'{tag} no script error', not r['errors'], r['errors'])
                            check(f'{tag} rendered', r['ready'] == f'{design}:case:{state}:{mode}:{layout}', r['ready'])
                            check(f'{tag} mode', r['editing'] == (mode == 'edit'))
                            check(f'{tag} no sideways spill', not r['spill'])
                            check(f'{tag} read shows no live control', mode == 'edit' or not r['liveControls'], r['liveControls'][:6])
                            if layout == 'tabs':
                                check(f'{tag} one tab shown', 'vehicle' in r['tabsActive'] and len(r['tabsActive']) <= 3, r['tabsActive'])
                            if design == 'proposal':
                                check(f'{tag} section row fits', r['navClipped'] <= 0 and r['lastVisible'], r['navClipped'])
                                check(f'{tag} marks only applied findings', set(r['changed']) <= set(r['applied']) | {'f50'}, sorted(set(r['changed']) - set(r['applied'])))
                                check(f'{tag} no account id in the spec origin', not r['guid'])
                                if r['cardBoxes']:
                                    check(f'{tag} card boxes 32px', all(h == 32 for h in r['cardBoxes']), r['cardBoxes'][:6])
                                check(f'{tag} no Not recorded segment', r['notRecordedSegments'] == 0, r['notRecordedSegments'])
                            else:
                                check(f'{tag} nothing marked', not r['changed'], r['changed'])
                            if width == 1580 and layout == 'scroll':
                                check(f'{tag} sticky aside fits', r['asideBottom'] is not None and r['asideBottom'] <= 1001, r['asideBottom'])
                            if width == 760 and design == 'today' and layout == 'scroll':
                                check(f'{tag} today clips the row at 760 (the finding)', r['navClipped'] > 0, r['navClipped'])
        # Today's clip at 1440 on the Audit Case is the finding.
        r = load('design=today&state=notready&mode=read', 1440, 900)
        check('today/notready@1440 clips the section row (f01 evidence)', r['navClipped'] > 0 or not r['lastVisible'], r['navClipped'])

        # Each finding alone on today changes only what it marks.
        for f in findings:
            if f['variable']:
                for opt in f['options'][1:]:
                    r = load(f"design=today&state=engineer&mode=edit&opt={f['variable']}:{opt}")
                    check(f"{f['id']} {opt} applies alone", f['id'] in r['applied'] and set(r['changed']) <= {f['id']}, r['changed'])
                continue
            tries = [(s, m) for s in (f['states'] or ['engineer', 'notready']) for m in ('edit', 'read') if s == 'engineer' or m == 'read']
            hit = None
            for st, mode in tries:
                r = load(f"design=today&state={st}&mode={mode}&opt={f['id']}:on")
                if f['id'] in r['applied']:
                    hit = (st, mode, r); break
            check(f"{f['id']} applies alone", hit is not None and set(hit[2]['changed']) <= {f['id']}, (tries, r['applied'], r['changed']))
            if hit:
                r = load(f"design=proposal&state={hit[0]}&mode={hit[1]}&opt={f['id']}:off")
                check(f"{f['id']} switches off", f['id'] not in r['applied'] and f['id'] not in r['changed'])
        # Operator variants.
        for opt in ['foot', 'head-link', 'inline']:
            r = load(f'design=proposal&state=engineer&mode=edit&opt=getval:{opt}')
            n = page.locator('[data-valuation-card="glasses"] :is(.btn, .link-button):has-text("Get valuation")').count()
            check(f'getval {opt} draws one Get valuation on Glass\'s', n == 1, n)
            check(f'getval {opt} moves the AI card button too', page.locator('.valuation-card-head .btn:has-text("Get valuation")').count() == 0)
        r = load('design=proposal&state=engineer&mode=edit')
        check('proposal default moves Get valuation out of the head', page.locator('.valuation-card-head .btn:has-text("Get valuation")').count() == 0 and page.locator('.v36-getval-foot').count() >= 1)
        check('proposal drops the blocker how line', page.locator('[data-next-action] .next-step-how').count() == 0)
        r = load('design=today&state=engineer&mode=edit')
        check('today keeps the blocker how line', page.locator('[data-next-action] .next-step-how').count() == 1)
        for opt in ['select', 'box', 'segments-none']:
            for mode in ['read', 'edit']:
                r = load(f'design=proposal&state=engineer&mode={mode}&opt=choice:{opt}')
                check(f'choice {opt} {mode} no Not recorded segment', r['notRecordedSegments'] == 0)
                if opt == 'select' and mode == 'edit':
                    check('choice select edit draws selects', page.locator('.decisions .dec select.fi').count() >= 2)
        for opt in ['indent-marker', 'indent', 'marker']:
            for mode in ['read', 'edit']:
                r = load(f'design=proposal&state=engineer&mode={mode}&section=estimate&opt=incl:{opt}')
                check(f'incl {opt} {mode} draws three included rows', page.locator('#section-estimate tr[data-v36-included]').count() == 3)
                notes = page.locator('#section-estimate .v36-incl-note').count()
                check(f'incl {opt} {mode} marker rows', notes == (3 if 'marker' in opt else 0), notes)
                check(f'incl {opt} {mode} no dash left on an included row', page.locator('#section-estimate tr[data-v36-included] .gv:text-is("\u2014")').count() == 0)
        r = load('design=today&state=engineer&mode=read&section=estimate')
        check('today draws the included rows as empty Other rows', page.locator('#section-estimate tr[data-v36-included]').count() == 3 and page.locator('#section-estimate tr[data-v36-included] .gv:text-is("\u2014")').count() >= 15)
        # f28 keeps read and edit the same height for Valuation.
        hr = load('design=proposal&state=engineer&mode=read&section=valuation')
        h_read = page.evaluate("document.querySelector('#section-valuation').getBoundingClientRect().height")
        load('design=proposal&state=engineer&mode=edit&section=valuation')
        h_edit = page.evaluate("document.querySelector('#section-valuation').getBoundingClientRect().height")
        check('f28 Valuation read and edit share a height (±48px, the AI card head control)', abs(h_read - h_edit) <= 48, (h_read, h_edit))
        load('design=today&state=engineer&mode=read&section=valuation')
        t_read = page.evaluate("document.querySelector('#section-valuation').getBoundingClientRect().height")
        load('design=today&state=engineer&mode=edit&section=valuation')
        t_edit = page.evaluate("document.querySelector('#section-valuation').getBoundingClientRect().height")
        check('today Valuation grows on Edit (the finding)', t_edit - t_read > 100, (t_read, t_edit))
        # Dialogs, viewer, widgets, pages.
        for d in DIALOGS:
            r = load(f'design=today&state=engineer&dialog={d}')
            check(f'dialog {d} opens', r['dialogsOpen'] == [d], r['dialogsOpen'])
        r = load('design=today&state=engineer&viewer=1')
        check('viewer opens', r['viewerOpen'])
        r = load('design=proposal&state=engineer&mode=edit&diff=1')
        check('diff legend lists the applied findings', page.locator('.v36-legend b').count() == len(r['applied']), (page.locator('.v36-legend b').count(), len(r['applied'])))
        load('design=proposal&state=engineer&split=1')
        check('split draws two panes', page.locator('.v36-pane').count() == 2)
        load('design=today&state=engineer&ruler=1')
        check('ruler marks heights', page.locator('[data-v36-h="36"]').count() > 20 and page.locator('[data-v36-h="32"]').count() > 5)
        r = load('design=today&state=engineer&mode=edit')
        check('today the Address input clips its text (f14 evidence)', r['addressClips'] is True, r['addressClips'])
        r = load('design=proposal&state=engineer&mode=edit')
        check('proposal the Address input fits', r['addressClips'] is False, r['addressClips'])
        load('design=today&state=engineer&mode=edit&ruler=1')
        check('ruler shows the 28px card boxes today', page.locator('[data-v36-h="28"]').count() >= 10)
        for pg in ['work-centre', 'cases']:
            r = load(f'design=today&page={pg}&busy=1')
            check(f'{pg} renders', r['ready'] == f'today:{pg}:engineer:read:scroll', r['ready'])
            check(f'{pg} busy stand-in draws', page.locator('.v36-busy-demo').count() >= 1, page.locator('.v36-busy-demo').count())
        r = load('design=today&state=engineer&busy=1')
        check('Case page busy stand-in draws', page.locator('.v36-busy-demo').count() >= 1)
        check('no console error', not console, console[:5])

        names = []
        if shots:
            n = 0

            def shot(name, q, width=1580, height=1000, full=False, before=None):
                nonlocal n
                n += 1
                load(q, width, height)
                if before:
                    page.evaluate(before)
                    page.wait_for_timeout(200)
                path = SHOTS / f'{n:02d}-{name}.png'
                page.screenshot(path=str(path), full_page=full)
                names.append(path.name)
                return path.name

            for design in ['today', 'proposal']:
                for state, modes in STATES.items():
                    for mode in modes:
                        for width, height in WIDTHS:
                            shot(f'{design}-{state}-{mode}-{width}', f'design={design}&state={state}&mode={mode}', width, height)
                for mode in ['read', 'edit']:
                    shot(f'{design}-engineer-{mode}-full', f'design={design}&state=engineer&mode={mode}', full=True)
                    for sec in ['claim', 'inspection', 'vehicle', 'damage', 'valuation', 'estimate', 'settlement', 'report', 'files', 'notes']:
                        shot(f'{design}-engineer-{mode}-{sec}', f'design={design}&state=engineer&mode={mode}&section={sec}')
                shot(f'{design}-notready-read-original-report', f'design={design}&state=notready&section=original-report')
                shot(f'{design}-engineer-read-tabs-vehicle', f'design={design}&state=engineer&layout=tabs&section=vehicle')
                shot(f'{design}-engineer-read-1440-strip', f'design={design}&state=engineer', 1440, 900)
            for opt in ['foot', 'head-link', 'inline']:
                shot(f'getval-{opt}', f'design=proposal&state=engineer&mode=edit&section=valuation&opt=getval:{opt}')
            for opt in ['select', 'box', 'segments-none']:
                for mode in ['read', 'edit']:
                    shot(f'choice-{opt}-{mode}', f'design=proposal&state=engineer&mode={mode}&section=settlement&opt=choice:{opt}')
            for opt in ['aligned', 'prefix']:
                shot(f'grid-{opt}', f'design=proposal&state=engineer&mode=edit&section=estimate&opt=grid:{opt}')
            shot('incl-today-edit', 'design=today&state=engineer&mode=edit&section=estimate')
            for opt in ['indent-marker', 'indent', 'marker']:
                for mode in ['read', 'edit']:
                    shot(f'incl-{opt}-{mode}', f'design=proposal&state=engineer&mode={mode}&section=estimate&opt=incl:{opt}')
            shot('diff-engineer-edit', 'design=proposal&state=engineer&mode=edit&diff=1')
            shot('split-engineer-read', 'design=proposal&state=engineer&split=1')
            shot('ruler-engineer-edit-valuation', 'design=today&state=engineer&mode=edit&section=valuation&ruler=1')
            shot('busy-work-centre', 'design=today&page=work-centre&busy=1')
            shot('busy-cases-list', 'design=today&page=cases&busy=1')
            shot('busy-case-next-action', 'design=today&state=review&busy=1')
            shot('landing-top', 'design=today&state=engineer&landing=top')
            shot('landing-section', 'design=today&state=engineer&section=settlement&landing=section')
            for d in DIALOGS:
                shot(f'dialog-{d}', f'design=today&state=engineer&dialog={d}')
            shot('viewer', 'design=today&state=engineer&viewer=1')
            shot('today-held-next-action', 'design=today&state=held')
            shot('proposal-held-next-action', 'design=proposal&state=held')
            shot('today-colleague-heads', 'design=today&state=colleague&section=claim')
            shot('proposal-colleague-heads', 'design=proposal&state=colleague&section=claim')
            shot('today-views-audit', 'design=today&state=views')
            shot('today-views-inspection', 'design=today&state=viewsinsp')
            print('shots', len(names))
        browser.close()

    result = {'date': date.today().isoformat(), 'fail': fail, 'okCount': len(ok), 'shots': names}
    (CURRENT / 'verification.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf8')
    print('RESULT', json.dumps({'fail': fail, 'okCount': len(ok)}))
    return 1 if fail else 0


if __name__ == '__main__':
    sys.exit(main())
