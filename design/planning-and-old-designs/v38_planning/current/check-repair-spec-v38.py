"""Self-check and screenshot capture for the v38 Repair Spec mockup.

    python check-repair-spec-v38.py            checks, then screenshots
    python check-repair-spec-v38.py --no-shots checks only

Loads the mockup in headless Chromium (Playwright) and asserts, for today
and the proposal, read and edit, both specs, at 1580, 1440 and 760: no
script error; the frame rendered; nothing spills sideways; read mode shows
no live control in the Repair Spec section; the proposal marks only the
proposals that are on and today marks nothing; each proposal alone on today
marks only itself and switches off on the proposal; P1 draws dashes for the
cells a type does not price and keeps an off-pattern value amber; P2 draws
the side-by-side table, Compare with current on every version row and a
print sheet that prints the dialog alone; P3 fills Supplementary on the
imported spec and Dismiss folds it; P4 previews the agreed sum (or offers
the button) and the Target % preview marks the lowered cells; P5 draws the
rate bar under the grid and takes the rate out of the head; each dialog
opens by name. Then it writes the numbered screenshots and
verification.json. This is evidence about the mockup, not the application.

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
SHOTS = CURRENT / 'v38-shots'
PAGE = CURRENT / 'pegasus_repair_spec_v38.html'
WIDTHS = [(1580, 1000), (1440, 900), (760, 1000)]
DIALOGS = ['versions-dialog', 'compare-estimates-dialog', 'delete-lines-dialog', 'delete-estimate-dialog']
VARIABLES = {'cols': ['dash'], 'cmp': ['side'], 'supp': ['auto'], 'agreed': ['preview', 'button'], 'rate': ['bar', 'both']}
IDS = {'cols': 'P1', 'cmp': 'P2', 'supp': 'P3', 'agreed': 'P4', 'rate': 'P5'}

ok, fail = [], []


def check(name, condition, detail=''):
    (ok if condition else fail).append(name if condition else f'{name} {detail}'.strip())


def url(q):
    return PAGE.as_uri() + '?' + q + '&strip=0'


READ = """() => {
  const sec = document.querySelector('#section-estimate');
  const record = document.querySelector('[data-case-record]');
  const text = (e) => e ? e.textContent.replace(/\\s+/g, ' ').trim() : '';
  const spill = [...document.querySelectorAll('.case-record, .case-record *')].some(e => {
    const s = getComputedStyle(e); return e.tagName !== 'INPUT' && e.scrollWidth > e.clientWidth + 1 && s.overflowX !== 'visible' && s.overflowX !== 'hidden' && !e.closest('.section-nav') && !e.closest('table') && !e.closest('.thumbs') && !e.closest('.grid') && !e.closest('.table-wrap'); });
  const liveControls = sec && !record.classList.contains('is-editing')
    ? [...sec.querySelectorAll('input:not([type=hidden]), select, textarea')].filter(e => e.offsetParent && e.type !== 'file').map(e => e.name || e.id || e.className)
    : [];
  return {
    errors: window.mockupErrors, ready: document.documentElement.dataset.ready, applied: window.v38Applied || [],
    editing: !!record && record.classList.contains('is-editing'), spill, liveControls,
    changed: [...document.querySelectorAll('[data-v38-change]')].map(e => e.dataset.v38Change).filter((v, i, a) => a.indexOf(v) === i),
    rows: sec.querySelectorAll('tr[data-estimate-line]').length,
    na: sec.querySelectorAll('td.na').length,
    viol: sec.querySelectorAll('.viol').length,
    previewed: sec.querySelectorAll('input.is-previewed').length,
    rateInHead: !!sec.querySelector('.est-head [data-estimate-rate-pair]'),
    rateBar: !!sec.querySelector('[data-estimate-rate-bar]'),
    headCells: sec.querySelectorAll('.est-head .fc').length,
    sumBox: !!sec.querySelector('[data-contract-sum]'),
    rescale: !!sec.querySelector('[data-contract-rescale]'),
    mismatch: text(sec.querySelector('[data-contract-mismatch]')),
    stmt: text(document.querySelector('[data-supplementary-statement]')),
    suppPanel: !!document.querySelector('[data-estimate-supplementary]'),
    suppOf: document.querySelector('[data-supp-of]') ? document.querySelector('[data-supp-of]').value : text(document.querySelector('[data-estimate-supplementary] .fv')),
    dismiss: !!document.querySelector('[data-supp-dismiss]'),
    side: !!document.querySelector('[data-estimate-compare-side]'),
    sideGroups: document.querySelectorAll('[data-estimate-compare-side] th.v38-cmp-group').length,
    diffRows: document.querySelectorAll('#compare-estimates-dialog [data-estimate-diff-table] tbody tr').length,
    versionCompare: document.querySelectorAll('[data-version-compare]').length,
    versionRows: document.querySelectorAll('[data-estimate-versions-table] tbody tr').length,
    printButton: !!document.querySelector('[data-compare-print]'),
    dialogsOpen: [...document.querySelectorAll('[data-dialog]:not([hidden])')].map(d => d.dataset.dialog),
    gross: text(document.querySelector('[data-estimate-gross]')),
    chip: text(document.querySelector('[data-repairs-of-value-chip]')),
    layoutSwitch: !!document.querySelector('[data-case-layout-switch]'),
    metricStrip: !!document.querySelector('.metric-strip'),
    guid: /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i.test(text(sec)),
    scaleRead: text(sec.querySelector('[data-scale-read]')),
    previewChip: (() => { const c = sec.querySelector('[data-scale-preview]'); return c ? !c.hidden : null; })(),
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
            page.wait_for_selector('html[data-ready]')
            page.wait_for_timeout(150)
            return page.evaluate(READ)

        for design in ['today', 'proposal']:
            for mode in ['read', 'edit']:
                for spec in ['1', '2']:
                    for width, height in WIDTHS:
                        tag = f'{design}/{mode}/spec{spec}@{width}'
                        r = load(f'design={design}&mode={mode}&spec={spec}', width, height)
                        check(f'{tag} no script error', not r['errors'], r['errors'])
                        check(f'{tag} rendered', r['ready'] == f'{design}:{mode}:{spec}', r['ready'])
                        check(f'{tag} mode', r['editing'] == (mode == 'edit'))
                        check(f'{tag} no sideways spill', not r['spill'])
                        check(f'{tag} read shows no live control', mode == 'edit' or not r['liveControls'], r['liveControls'][:6])
                        check(f'{tag} rows drawn', r['rows'] == (14 if spec == '1' else 17), r['rows'])
                        check(f'{tag} no account id in the spec origin', not r['guid'])
                        check(f'{tag} frame patched', not r['layoutSwitch'] and not r['metricStrip'])
                        check(f'{tag} ribbon chip', r['chip'].startswith('Repairs ') and r['chip'].endswith('% of value'), r['chip'])
                        if design == 'proposal':
                            check(f'{tag} marks only the proposals on', set(r['changed']) <= set(r['applied']), sorted(set(r['changed']) - set(r['applied'])))
                            check(f'{tag} P1 dashes', r['na'] > 0, r['na'])
                            check(f'{tag} P5 rate bar, not in head', r['rateBar'] and not r['rateInHead'] and r['headCells'] == 4, (r['rateBar'], r['rateInHead'], r['headCells']))
                            if spec == '2':
                                check(f'{tag} P3 statement on the imported spec', r['stmt'].startswith('Following receipt of a supplementary estimate'), r['stmt'][:40])
                                check(f'{tag} P3 changes vs the sent version', 'v2' in r['suppOf'] or 'spec-1:v2' in r['suppOf'], r['suppOf'])
                            else:
                                check(f'{tag} P3 nothing filled on the earlier spec', not r['stmt'], r['stmt'][:40])
                        else:
                            check(f'{tag} nothing marked', not r['changed'], r['changed'])
                            check(f'{tag} no dashes today', r['na'] == 0, r['na'])
                            check(f'{tag} rate in the head today', r['rateInHead'] and not r['rateBar'] and r['headCells'] == 6, r['headCells'])
                            check(f'{tag} Supplementary unfilled today', not r['stmt'])

        # The off-pattern value (a unit amount on a Repair line) stays amber under P1.
        for design in ['today', 'proposal']:
            for mode in ['read', 'edit']:
                r = load(f'design={design}&mode={mode}&spec=2')
                check(f'{design}/{mode} off-pattern cell amber', r['viol'] == 1, r['viol'])

        # Each proposal alone on today marks only itself; off on the proposal it marks nothing of its own.
        for variable, options in VARIABLES.items():
            pid = IDS[variable]
            for opt in options:
                r = load(f'design=today&mode=edit&spec=2&opt={variable}:{opt}')
                check(f'{pid} {opt} applies alone', r['applied'] == [pid] and set(r['changed']) <= {pid}, (r['applied'], r['changed']))
            off = 'head' if variable == 'rate' else 'today'
            r = load(f'design=proposal&mode=edit&spec=2&opt={variable}:{off}')
            check(f'{pid} switches off', pid not in r['applied'] and pid not in r['changed'], (r['applied'], r['changed']))

        # P1: the dash count follows the type map; a Replace line keeps its boxes.
        r = load('design=proposal&mode=edit&spec=1')
        check('P1 spec 1 dashes (14 lines)', r['na'] == 40, r['na'])
        n_replace_dash = page.evaluate("[...document.querySelectorAll('#section-estimate tr[data-estimate-line]')].filter(tr => tr.querySelector('select').value === 'Replace').reduce((n, tr) => n + tr.querySelectorAll('td.na').length, 0)")
        check('P1 a Replace line dashes only Paint h', n_replace_dash == 4, n_replace_dash)
        n_ri = page.evaluate("[...document.querySelectorAll('#section-estimate tr[data-estimate-line]')].filter(tr => tr.querySelector('select').value === 'RemoveAndRefit').map(tr => tr.querySelectorAll('td.na').length)")
        check('P1 an R&I line keeps Hours only', n_ri == [5, 5], n_ri)

        # P2: side by side, Compare with current, the print sheet.
        r = load('design=proposal&mode=edit&spec=2&dialog=compare-estimates-dialog')
        check('P2 compare opens side by side', r['dialogsOpen'] == ['compare-estimates-dialog'] and r['side'] and r['sideGroups'] == 2, (r['dialogsOpen'], r['side'], r['sideGroups']))
        check('P2 compare lists every line (added, changed, removed, same)', r['diffRows'] == 18, r['diffRows'])
        check('P2 print button', r['printButton'])
        from_label = page.evaluate("document.querySelector('[data-cmp-from]').selectedOptions[0].textContent")
        check('P2 From defaults to the sent version', 'Sent on report' in from_label, from_label)
        page.emulate_media(media='print')
        page.evaluate("const d = document.querySelector('#compare-estimates-dialog'); document.body.append(d); document.body.classList.add('v38-printing')")
        visible = page.evaluate("""() => { const vis = (e) => e && getComputedStyle(e).display !== 'none' && e.offsetParent !== null; return { ribbon: vis(document.querySelector('.ribbon')), grid: vis(document.querySelector('#section-estimate .grid table')), table: vis(document.querySelector('[data-estimate-compare-side]')), form: vis(document.querySelector('[data-estimate-compare-form]')) }; }""")
        check('P2 print sheet prints the comparison alone', visible['table'] and not visible['ribbon'] and not visible['grid'] and not visible['form'], visible)
        page.emulate_media(media='screen')
        r = load('design=proposal&mode=edit&spec=1&dialog=versions-dialog')
        check('P2 Compare with current on every version row', r['versionCompare'] == 2 and r['versionRows'] == 3, (r['versionCompare'], r['versionRows']))
        page.click('[data-version-compare="1"]')
        page.wait_for_timeout(150)
        r = page.evaluate(READ)
        check('P2 Compare with current opens Compare on v1 → Working', r['dialogsOpen'] == ['compare-estimates-dialog'] and page.evaluate("document.querySelector('[data-cmp-from]').value") == 'spec-1:v1' and page.evaluate("document.querySelector('[data-cmp-to]').value") == 'spec-1:working', r['dialogsOpen'])
        r = load('design=today&mode=edit&spec=2&dialog=compare-estimates-dialog')
        check('today compare is spec to spec, no side table', not r['side'] and not r['printButton'] and r['versionCompare'] == 0)
        page.select_option('[data-cmp-from]', 'spec-1:working'); page.select_option('[data-cmp-to]', 'spec-2:working'); page.click('[data-estimate-compare-go]'); page.wait_for_timeout(150)
        r = page.evaluate(READ)
        check('today compare lists added, changed and removed only', r['diffRows'] == 6 and not r['side'], r['diffRows'])

        # P3: Dismiss folds the panel; today the panel waits for a choice.
        r = load('design=proposal&mode=edit&spec=2')
        check('P3 Dismiss offered while editing', r['dismiss'])
        page.click('[data-supp-dismiss]'); page.wait_for_timeout(150)
        r = page.evaluate(READ)
        check('P3 Dismiss folds the panel', not r['suppPanel'] and not r['stmt'])
        r = load('design=proposal&mode=read&spec=2')
        check('P3 read shows the filled panel without Dismiss', r['stmt'].startswith('Following') and not r['dismiss'])
        r = load('design=today&mode=edit&spec=2')
        check('today Supplementary waits for Changes vs', r['suppPanel'] and not r['stmt'])
        page.select_option('[data-supp-of]', 'spec-1:working'); page.wait_for_timeout(150)
        r = page.evaluate(READ)
        check('today Changes vs a spec fills the statement', r['stmt'].startswith('Following'))

        # P4: the agreed sum previews, or the button does.
        r = load('design=proposal&mode=edit&spec=2&sum=2750')
        check('P4 preview: the sum previews the scaling', r['previewed'] > 0 and r['gross'] == '£2,750.00' and r['previewChip'] is True and r['sumBox'], (r['previewed'], r['gross'], r['previewChip']))
        check('P4 preview: no mismatch while previewed', r['mismatch'] == '', r['mismatch'])
        r = load('design=proposal&mode=edit&spec=2&sum=2750&opt=agreed:button')
        check('P4 button: the chip and Rescale to agreed sum, nothing previewed', r['rescale'] and r['mismatch'].startswith('Spec ') and r['previewed'] == 0, (r['rescale'], r['mismatch'], r['previewed']))
        page.click('[data-contract-rescale]'); page.wait_for_timeout(150)
        r = page.evaluate(READ)
        check('P4 button previews on press', r['previewed'] > 0 and r['gross'] == '£2,750.00', (r['previewed'], r['gross']))
        r = load('design=today&mode=edit&spec=2&sum=2750')
        check('today: the sum is read from Decisions, the chip states the mismatch', not r['sumBox'] and not r['rescale'] and r['mismatch'].startswith('Spec ') and r['previewed'] == 0, (r['sumBox'], r['mismatch']))
        # The Target % preview (live behaviour) marks the lowered cells and reads out.
        r = load('design=today&mode=edit&spec=2&scale=60')
        check('Target % preview marks cells and reads out', r['previewed'] > 0 and 'prices ×' in r['scaleRead'] and r['previewChip'] is True, (r['previewed'], r['scaleRead']))
        page.click('[data-scale-apply]'); page.wait_for_timeout(150)
        r = page.evaluate(READ)
        check('Apply records the scaled spec and offers Remove scaling', r['previewed'] == 0 and page.locator('[data-estimate-scaled]').count() == 1 and page.locator('[data-scale-remove]').count() == 1)

        # P5: both.
        r = load('design=proposal&mode=edit&spec=2&opt=rate:both')
        check('P5 both: head and bar', r['rateInHead'] and r['rateBar'] and r['headCells'] == 6)
        r = load('design=proposal&mode=read&spec=2')
        check('P5 read: the bar reads as values', r['rateBar'] and not r['liveControls'])

        # Dialogs by name; Split; Diff.
        for d in DIALOGS:
            r = load(f'design=today&mode=edit&spec=1&dialog={d}')
            check(f'dialog {d} opens', r['dialogsOpen'] == [d], r['dialogsOpen'])
        r = load('design=proposal&mode=edit&spec=2&diff=1')
        check('diff legend lists the proposals on', page.locator('.v38-legend b').count() == len(r['applied']), (page.locator('.v38-legend b').count(), len(r['applied'])))
        load('design=proposal&mode=edit&spec=2&split=1')
        check('split draws two panes', page.locator('.v38-pane').count() == 2)
        # The grid edits: a type change redraws the cells (P1), a removed line can be undone.
        load('design=proposal&mode=edit&spec=1')
        before = page.evaluate("document.querySelectorAll('#section-estimate td.na').length")
        page.select_option('#line-operation-4', 'Replace'); page.wait_for_timeout(150)
        after = page.evaluate("document.querySelectorAll('#section-estimate td.na').length")
        check('P1 a type change redraws the cells (a Repair line made Replace opens Part no., Qty and Unit)', after == before - 3, (before, after))
        page.click('[data-remove-line="1"]'); page.wait_for_timeout(150)
        check('remove line offers Undo', page.locator('[data-undo-line]').count() == 1 and page.evaluate("document.querySelectorAll('#section-estimate tr[data-estimate-line]').length") == 13)
        page.click('[data-undo-line]'); page.wait_for_timeout(150)
        check('Undo restores the line', page.evaluate("document.querySelectorAll('#section-estimate tr[data-estimate-line]').length") == 14)
        check('no console error', not console, console[:5])

        names = []
        if shots:
            n = 0

            def shot(name, q, width=1580, height=1000, full=False):
                nonlocal n
                n += 1
                load(q, width, height)
                path = SHOTS / f'{n:02d}-{name}.png'
                page.screenshot(path=str(path), full_page=full)
                names.append(path.name)

            for design in ['today', 'proposal']:
                for mode in ['read', 'edit']:
                    for width, height in WIDTHS:
                        shot(f'{design}-{mode}-spec2-{width}', f'design={design}&mode={mode}&spec=2', width, height)
                    shot(f'{design}-{mode}-spec1', f'design={design}&mode={mode}&spec=1')
                shot(f'{design}-edit-spec2-full', f'design={design}&mode=edit&spec=2', full=True)
                shot(f'{design}-compare', f'design={design}&mode=edit&spec=2&dialog=compare-estimates-dialog' + ('' if design == 'proposal' else '&cmpfrom=spec-1:working&cmpto=spec-2:working'))
                shot(f'{design}-versions', f'design={design}&mode=edit&spec=1&dialog=versions-dialog')
                shot(f'{design}-sum', f'design={design}&mode=edit&spec=2&sum=2750')
                shot(f'{design}-scale', f'design={design}&mode=edit&spec=2&scale=60')
            shot('agreed-button', 'design=proposal&mode=edit&spec=2&sum=2750&opt=agreed:button')
            shot('rate-both', 'design=proposal&mode=edit&spec=2&opt=rate:both')
            shot('rate-head', 'design=proposal&mode=edit&spec=2&opt=rate:head')
            shot('cols-today-on-proposal', 'design=proposal&mode=edit&spec=2&opt=cols:today')
            shot('supp-read', 'design=proposal&mode=read&spec=2')
            shot('diff-edit', 'design=proposal&mode=edit&spec=2&diff=1')
            shot('split-edit', 'design=proposal&mode=edit&spec=2&split=1')
            shot('split-read', 'design=proposal&mode=read&spec=2&split=1')
            load('design=proposal&mode=edit&spec=2&dialog=compare-estimates-dialog')
            page.emulate_media(media='print'); page.evaluate("const d = document.querySelector('#compare-estimates-dialog'); document.body.append(d); document.body.classList.add('v38-printing')"); page.wait_for_timeout(150)
            n += 1; path = SHOTS / f'{n:02d}-print-sheet.png'; page.screenshot(path=str(path), full_page=True); names.append(path.name)
            page.emulate_media(media='screen')
            for d in ['delete-lines-dialog', 'delete-estimate-dialog']:
                shot(f'dialog-{d}', f'design=today&mode=edit&spec=1&dialog={d}')
            print('shots', len(names))
        browser.close()

    result = {'date': date.today().isoformat(), 'fail': fail, 'okCount': len(ok), 'shots': names}
    (CURRENT / 'verification.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf8')
    print('RESULT', json.dumps({'fail': fail, 'okCount': len(ok)}))
    return 1 if fail else 0


if __name__ == '__main__':
    sys.exit(main())
