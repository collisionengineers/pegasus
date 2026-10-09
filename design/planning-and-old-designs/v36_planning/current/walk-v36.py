"""Live walk of the Case page for the v36 round.

    python walk-v36.py <caseId> <tag> [--base https://localhost:5240] [--edit] [--no-shots]
    python walk-v36.py --hold <caseId>          # places the Case on hold through the UI
    python walk-v36.py --measure <caseId>       # prints the geometry table only

Opens the running visual host (artifacts/ui-baseline-review/visual-host) in
headless Chromium, lets site.js and case-workspace.js run, and records for
the given Case and tag:

  captured/frame-<tag>-read.html      the post-script DOM (outerHTML)
  captured/frame-<tag>-edit.html      the same inside the edit session (--edit)
  captured/measure-<tag>-<mode>.json  geometry: sticky block, ribbon cells,
                                      section row, aside, panel heads, cells
  live-shots/<tag>-<mode>-<width>.png viewport shots at 1580, 1440 and 760
  live-shots/<tag>-<mode>-full.png    one whole-page shot at 1580
  live-shots/<tag>-<mode>-sec-<key>-1580.png  each section scrolled into view
  live-shots/<tag>-<mode>-tabs-<key>-1580.png each section in Tabs layout
  live-shots/<tag>-<mode>-dialog-<id>-1580.png each dialog open
  live-shots/<tag>-<mode>-menu-1580.png   the Actions menu open

This is evidence about the running synthetic host, not the application in
production. Set PEGASUS_CHROME when Playwright's own browser does not match
its driver.
"""
import json
import os
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

CURRENT = Path(__file__).resolve().parent
CAPTURED = CURRENT / 'captured'
SHOTS = CURRENT / 'live-shots'
WIDTHS = [(1580, 1000), (1440, 900), (760, 1000)]

MEASURE = """() => {
  const r = (el) => { if (!el) return null; const b = el.getBoundingClientRect(); return { x: Math.round(b.x), y: Math.round(b.y), w: Math.round(b.width), h: Math.round(b.height) }; };
  const text = (el) => el ? el.textContent.replace(/\\s+/g, ' ').trim() : null;
  const record = document.querySelector('[data-case-record]');
  const sticky = document.querySelector('.sticky-block');
  const ribbon = document.querySelector('.ribbon');
  const row = document.querySelector('.section-row');
  const nav = document.querySelector('.section-nav');
  const aside = document.querySelector('aside.workspace-aside');
  const out = {
    url: location.href,
    editing: !!record && record.classList.contains('is-editing'),
    layout: record ? record.dataset.layout : null,
    stickyH: getComputedStyle(record || document.documentElement).getPropertyValue('--sticky-h').trim(),
    sticky: r(sticky), ribbon: r(ribbon), row: r(row), nav: r(nav),
    navOverflow: nav ? nav.scrollWidth - nav.clientWidth : null,
    navLinks: [...document.querySelectorAll('.section-link')].map(a => ({ t: text(a), ...r(a), current: a.getAttribute('aria-current') })),
    ribbonItems: [...document.querySelectorAll('.ribbon .ribbon-item')].map(e => ({ label: text(e.querySelector('.ribbon-label')), value: text(e.querySelector('.ribbon-value')), ...r(e) })),
    chips: [...document.querySelectorAll('.ribbon-chips .status')].map(e => ({ t: text(e), ...r(e) })),
    chipsBox: r(document.querySelector('.ribbon-chips')),
    actions: [...document.querySelectorAll('.ribbon-actions > *, .ribbon-actions form > *')].filter(e => e.offsetParent).map(e => ({ tag: e.tagName.toLowerCase(), t: text(e).slice(0, 40), ...r(e) })),
    aside: r(aside),
    asideScroll: aside ? { sh: aside.scrollHeight, ch: aside.clientHeight, top: getComputedStyle(aside).top, maxH: getComputedStyle(aside).maxHeight } : null,
    asideCards: aside ? [...aside.querySelectorAll(':scope > section')].map(s => ({ t: text(s.querySelector('h2')), ...r(s), head: r(s.querySelector('.panel-head')) })) : [],
    sections: [...document.querySelectorAll('.record-section')].map(s => ({
      key: s.dataset.section, lazy: s.classList.contains('section-placeholder'), ...r(s),
      head: r(s.querySelector(':scope > .panel-head')),
      headControls: [...(s.querySelector(':scope > .panel-head .panel-actions')?.children || [])].filter(e => e.offsetParent).map(e => ({ tag: e.tagName.toLowerCase(), t: text(e).slice(0, 40), ...r(e) })),
      subPanels: [...s.querySelectorAll('.sub-panel')].map(p => ({ t: text(p.querySelector('h3')), ...r(p), head: r(p.querySelector('h3')) })),
      cells: [...s.querySelectorAll('.fc')].filter(c => c.offsetParent).map(c => {
        const box = c.querySelector('.fv:not([style*="display: none"]), .fi');
        const shown = [...c.querySelectorAll('.fv, .fi')].find(e => getComputedStyle(e).display !== 'none');
        return { label: text(c.querySelector('label, .lbl')), ro: c.classList.contains('ro'), kind: shown ? shown.tagName.toLowerCase() + '.' + shown.className.split(' ')[0] : null, ...r(shown || box), value: text(shown)?.slice(0, 30) };
      }),
      controls: [...s.querySelectorAll('button, a.btn, input:not([type=hidden]), select, textarea')].filter(e => e.offsetParent && !e.closest('.fc')).map(e => ({ tag: e.tagName.toLowerCase(), type: e.type || null, t: text(e).slice(0, 30) || e.getAttribute('aria-label') || e.name, h: Math.round(e.getBoundingClientRect().height), w: Math.round(e.getBoundingClientRect().width), disabled: e.disabled || e.getAttribute('aria-disabled') === 'true' })),
    })),
    heights: {},
    dialogs: [...document.querySelectorAll('[data-dialog]')].map(d => ({ id: d.id, title: text(d.querySelector('h2')), hidden: d.hidden })),
    menuItems: [...document.querySelectorAll('.ribbon-actions .menu-body *')].filter(e => e.matches('button, a') ).map(e => ({ t: text(e), disabled: e.disabled, gated: !!e.closest('.menu-gated') })),
    notices: [...document.querySelectorAll('[data-case-notices] .notice')].map(text),
    docHeight: document.documentElement.scrollHeight,
  };
  const ladder = {};
  for (const e of document.querySelectorAll('button, a.btn, input:not([type=hidden]):not([type=checkbox]):not([type=radio]), select, textarea, .fv, .status, .tab, .section-link')) {
    if (!e.offsetParent) continue;
    const h = Math.round(e.getBoundingClientRect().height);
    const k = e.tagName.toLowerCase() + (e.className ? '.' + String(e.className).split(' ').filter(Boolean).slice(0, 2).join('.') : '');
    (ladder[h] ||= {})[k] = (ladder[h][k] || 0) + 1;
  }
  out.heights = ladder;
  return out;
}"""


def shoot(page, path):
    page.screenshot(path=str(path))
    return path.name


def settle(page, ms=600):
    page.wait_for_timeout(ms)


def capture(page, tag, mode, shots):
    CAPTURED.mkdir(exist_ok=True)
    SHOTS.mkdir(exist_ok=True)
    # Load every lazy section before the dump.
    page.evaluate("""async () => {
      for (let i = 0; i < 6; i++) {
        for (const p of document.querySelectorAll('.section-placeholder[data-lazy]')) p.scrollIntoView({block: 'center', behavior: 'instant'});
        await new Promise(r => setTimeout(r, 400));
        if (!document.querySelector('.section-placeholder[data-lazy]')) break;
      }
      window.scrollTo({top: 0, behavior: 'instant'});
    }""")
    settle(page, 800)
    html = page.evaluate('document.documentElement.outerHTML')
    (CAPTURED / f'frame-{tag}-{mode}.html').write_text('<!doctype html>\n' + html, encoding='utf8')
    measures = {}
    names = []
    for width, height in WIDTHS:
        page.set_viewport_size({'width': width, 'height': height})
        settle(page, 400)
        page.evaluate("window.scrollTo({top: 0, behavior: 'instant'})")
        settle(page, 200)
        measures[width] = page.evaluate(MEASURE)
        if shots:
            names.append(shoot(page, SHOTS / f'{tag}-{mode}-{width}.png'))
    page.set_viewport_size({'width': 1580, 'height': 1000})
    settle(page, 300)
    if shots:
        page.screenshot(path=str(SHOTS / f'{tag}-{mode}-full.png'), full_page=True)
        keys = page.evaluate("[...document.querySelectorAll('.record-section')].map(s => s.dataset.section)")
        for key in keys:
            page.evaluate(f"document.querySelector('#section-{key}')?.scrollIntoView({{block: 'start', behavior: 'instant'}})")
            settle(page, 350)
            names.append(shoot(page, SHOTS / f'{tag}-{mode}-sec-{key}-1580.png'))
        page.evaluate("window.scrollTo({top: 0, behavior: 'instant'})")
    (CAPTURED / f'measure-{tag}-{mode}.json').write_text(json.dumps(measures, indent=1), encoding='utf8')
    return names


def open_menu(page, tag, mode):
    page.click('.ribbon-actions details.menu > summary')
    settle(page, 300)
    name = shoot(page, SHOTS / f'{tag}-{mode}-menu-1580.png')
    items = page.evaluate("[...document.querySelectorAll('.ribbon-actions .menu-body button, .ribbon-actions .menu-body a')].map(e => e.textContent.replace(/\\s+/g,' ').trim())")
    page.keyboard.press('Escape')
    settle(page, 200)
    return name, items


def open_dialogs(page, tag, mode):
    names = []
    ids = page.evaluate("[...document.querySelectorAll('[data-dialog]')].map(d => d.id).filter(Boolean)")
    for id_ in ids:
        opened = page.evaluate(f"""() => {{
          const opener = document.querySelector('[data-dialog-open="{id_}"]:not([disabled])');
          if (!opener) return false;
          const menu = opener.closest('details.menu'); if (menu) menu.open = true;
          opener.click(); return true; }}""")
        if not opened:
            continue
        settle(page, 400)
        names.append(shoot(page, SHOTS / f'{tag}-{mode}-dialog-{id_}-1580.png'))
        page.keyboard.press('Escape')
        settle(page, 300)
        page.evaluate("document.querySelectorAll('details.menu[open]').forEach(d => d.open = false)")
    return names


def tabs(page, tag, mode):
    names = []
    page.click('.layout-switch button:has-text("Tabs")')
    settle(page, 400)
    keys = page.evaluate("[...document.querySelectorAll('.section-link')].map(a => a.dataset.sectionLink || a.getAttribute('href').split('#section-')[1])")
    for key in keys:
        page.click(f'.section-link[href*="#section-{key}"]')
        settle(page, 500)
        names.append(shoot(page, SHOTS / f'{tag}-{mode}-tabs-{key}-1580.png'))
    page.click('.layout-switch button:has-text("Scroll")')
    settle(page, 400)
    return names


def enter_edit(page):
    page.click('.ribbon-actions [data-case-edit]')
    page.wait_for_function("document.querySelector('[data-case-record]')?.classList.contains('is-editing')", timeout=15000)
    settle(page, 800)


def leave_edit(page):
    page.click('.ribbon-actions button[form="case-finish-editing-form"]')
    page.wait_for_function("!document.querySelector('[data-case-record]')?.classList.contains('is-editing')", timeout=15000)
    settle(page, 500)


def main():
    args = sys.argv[1:]
    base = 'https://localhost:5240'
    if '--base' in args:
        base = args[args.index('--base') + 1]
    shots = '--no-shots' not in args
    case_id = next(a for a in args if not a.startswith('--') and a not in (base,))
    tag = [a for a in args if not a.startswith('--') and a != case_id and a != base]
    tag = tag[0] if tag else 'case'
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=os.environ.get('PEGASUS_CHROME') or None)
        context = browser.new_context(viewport={'width': 1580, 'height': 1000}, ignore_https_errors=True)
        page = context.new_page()
        errors = []
        page.on('pageerror', lambda e: errors.append(str(e)))
        page.on('console', lambda m: errors.append('console: ' + m.text) if m.type == 'error' else None)
        page.goto(f'{base}/Cases/{case_id}')
        page.wait_for_load_state('networkidle')
        settle(page, 800)
        if '--hold' in args:
            page.evaluate("document.querySelector('.ribbon-actions details.menu').open = true")
            page.click('[data-dialog-open="case-hold-dialog"]')
            page.fill('#case-hold-dialog textarea', 'v36 walk: held for the chip')
            page.fill('#case-hold-dialog input[type=date]', '2031-05-20')
            page.click('#case-hold-dialog button[type=submit]:has-text("Place on Hold")')
            page.wait_for_load_state('networkidle')
            print('held', page.url)
            browser.close()
            return
        if '--measure' in args:
            print(json.dumps(page.evaluate(MEASURE), indent=1))
            browser.close()
            return
        log = {'tag': tag, 'url': page.url, 'read': {}, 'edit': {}}
        log['read']['shots'] = capture(page, tag, 'read', shots)
        if shots:
            log['read']['menu'], log['read']['menuItems'] = open_menu(page, tag, 'read')
            log['read']['dialogs'] = open_dialogs(page, tag, 'read')
            log['read']['tabs'] = tabs(page, tag, 'read')
        if '--edit' in args:
            enter_edit(page)
            log['edit']['shots'] = capture(page, tag, 'edit', shots)
            if shots:
                log['edit']['menu'], log['edit']['menuItems'] = open_menu(page, tag, 'edit')
                log['edit']['dialogs'] = open_dialogs(page, tag, 'edit')
                log['edit']['tabs'] = tabs(page, tag, 'edit')
            leave_edit(page)
        log['errors'] = errors
        (CAPTURED / f'walk-{tag}.json').write_text(json.dumps(log, indent=1), encoding='utf8')
        print(json.dumps({'tag': tag, 'errors': errors, 'readShots': len(log['read'].get('shots', [])), 'editShots': len(log['edit'].get('shots', []))}))
        browser.close()


if __name__ == '__main__':
    main()
