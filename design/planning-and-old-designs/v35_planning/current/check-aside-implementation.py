"""Stage 2 conformance check for the implemented Case aside (v35 design 8).

    python check-aside-implementation.py <folder of captured pages>

The folder holds the Case pages the application rendered after Stage 2, made
by a temporary test (captured/capture-implementation-test.cs.txt, never
committed) that wrote GetHtmlAsync output for each state to the folder named
by PEGASUS_V35_CAPTURE. The script restyles those pages with the live CSS
from this checkout, runs the real site.js and case-workspace.js with fetch
stubbed, serves them from localhost so the fold cookie works, and:

  1. checks Next action holds one step, the Report not ready card lists
     everything outstanding, folded until opened, opened and folded again
     by its chevron, remembered across a reload through the fold cookie,
     while a section fold keeps its own default-open meaning;
  2. screenshots the page in each state at 1580, 1440 and 760, closed, and
     opened at 1580, into v35-conformance/ beside the mockup's r4 shots.

The pages are the application's own markup and scripts, opened with stubbed
requests: this is not a signed-in walk of a running Pegasus. Set
PEGASUS_CHROME to a Chromium executable when Playwright's own browser does
not match its driver.
"""
import base64
import functools
import http.server
import json
import os
import re
import sys
import tempfile
import threading
from datetime import date
from pathlib import Path

from playwright.sync_api import sync_playwright

CURRENT = Path(__file__).resolve().parent
ROOT = CURRENT.parents[3]
WEB = ROOT / 'src' / 'Pegasus.Web' / 'wwwroot'
RAW = Path(sys.argv[1]).resolve()
OUT = Path(tempfile.mkdtemp(prefix='v35-conformance-'))
SHOTS = CURRENT / 'v35-conformance'
SHOTS.mkdir(exist_ok=True)
STATES = ['notready', 'review', 'engineer', 'ready']
WIDTHS = [(1580, 1000), (1440, 900), (760, 1000)]
FOLD = 'case.aside.report'

STUB = r"""
window.fetch = function () { return new Promise(function () {}); };
window.__errors = [];
window.addEventListener('error', function (e) { window.__errors.push(e.message); });
"""


def css():
    site = (WEB / 'css' / 'site.css').read_text(encoding='utf8')
    regular = base64.b64encode((WEB / 'fonts' / 'inter' / 'InterVariable.woff2').read_bytes()).decode()
    italic = base64.b64encode((WEB / 'fonts' / 'inter' / 'InterVariable-Italic.woff2').read_bytes()).decode()
    site = re.sub(r'url\(\.\./fonts/inter/InterVariable\.[0-9a-z]+\.woff2\)', lambda m: f'url(data:font/woff2;base64,{regular})', site)
    site = site.replace('url(../fonts/inter/InterVariable-Italic.woff2)', f'url(data:font/woff2;base64,{italic})')
    return site + '\n' + (WEB / 'css' / 'case-workspace.css').read_text(encoding='utf8')


def page_html(name):
    html = (RAW / f'{name}.html').read_text(encoding='utf8')
    html = re.sub(r'<link rel="stylesheet"[^>]*>', '', html)
    html = html.replace('</head>', '<style>' + css() + '</style></head>', 1)
    mark = base64.b64encode((WEB / 'images' / 'pegasus-mark-refined-128.png').read_bytes()).decode()
    html = re.sub(r'src="/images/pegasus-mark-refined-128\.[0-9a-z]+\.png"', lambda m: f'src="data:image/png;base64,{mark}"', html)

    def inline(match):
        file = re.sub(r'\.[0-9a-z]+\.js$', '.js', match.group(1))
        path = WEB / 'js' / file
        if not path.exists():
            return ''
        return '<script>' + path.read_text(encoding='utf-8-sig').replace('</script', '<\\/script') + '</script>'

    html = re.sub(r'<script src="/js/([^"]+)"[^>]*></script>', inline, html)
    html = html.replace('<head>', '<head><script>' + STUB + '</script>', 1)
    (OUT / f'{name}.html').write_text(html, encoding='utf8')


ok, fail = [], []


def check(name, condition, detail=''):
    (ok if condition else fail).append(name if condition else f'{name} {detail}'.strip())


READ = """() => {
  const aside = document.querySelector('aside[data-case-aside]');
  const next = aside.querySelector('[data-next-action]');
  const card = aside.querySelector('[data-report-not-ready]');
  const body = card ? card.querySelector('.panel-body') : null;
  return {
    errors: window.__errors,
    labels: next ? next.querySelectorAll('[data-next-label]').length : -1,
    nextItems: next ? next.querySelectorAll('[data-report-blocker], [data-case-requirement]').length : -1,
    nextLists: next ? next.querySelectorAll('.blocker-list').length : -1,
    nextText: next ? next.textContent.replace(/\\s+/g, ' ').trim() : '',
    stepControl: next ? [...next.querySelectorAll('.next-step-go')].map(e => e.textContent.trim()) : [],
    card: !!card,
    cardHeading: card ? card.querySelector('.panel-head h2').textContent.trim() : '',
    folded: card ? card.classList.contains('is-collapsed') : null,
    bodyShown: body ? getComputedStyle(body).display !== 'none' : null,
    blockers: card ? card.querySelectorAll('[data-report-blocker]').length : 0,
    requirements: card ? [...card.querySelectorAll('[data-case-requirement]')].map(li => [li.querySelector('a').textContent.trim(), li.querySelector('a').getAttribute('href')]) : [],
    groups: card ? [...card.querySelectorAll('.blocker-group-head')].map(e => e.textContent.trim()) : [],
    amberFill: card ? [...card.querySelectorAll('.blocker')].some(li => getComputedStyle(li).backgroundColor !== 'rgba(0, 0, 0, 0)') : false,
    spill: [aside, ...aside.querySelectorAll('*')].some(e => {
      const s = getComputedStyle(e); return e.scrollWidth > e.clientWidth + 1 && s.overflowX !== 'visible' && s.overflowX !== 'hidden'; }),
    cookie: document.cookie,
  };
}"""


def main():
    for name in STATES:
        page_html(name)
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(OUT))
    http.server.SimpleHTTPRequestHandler.log_message = lambda *a: None
    server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    base = f'http://127.0.0.1:{server.server_port}'

    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=os.environ.get('PEGASUS_CHROME') or None)
        context = browser.new_context(viewport={'width': 1580, 'height': 1000})
        page = context.new_page()
        console = []
        page.on('pageerror', lambda e: console.append(str(e)))

        def load(name, width=1580, height=1000):
            page.set_viewport_size({'width': width, 'height': height})
            page.goto(f'{base}/{name}.html')
            page.wait_for_load_state('load')
            return page.evaluate(READ)

        for name in STATES:
            context.clear_cookies()
            r = load(name)
            check(f'{name} no script error', not r['errors'], r['errors'])
            check(f'{name} Next action holds one step', r['labels'] <= 1 and r['nextItems'] <= 1 and r['nextLists'] == 0, r)
            check(f'{name} no sideways spill', not r['spill'])
            if name == 'ready':
                check('ready names its delivery step above Report', r['labels'] == 1 and 'Send report' in r['nextText'] and r['stepControl'] == ['Report'], r['nextText'])
                check('ready has no card', not r['card'])
                continue
            check(f'{name} card present', r['card'])
            check(f'{name} card folded at first', r['folded'] is True and r['bodyShown'] is False, r)
            check(f'{name} card lists blockers', r['blockers'] > 0, r['blockers'])
            check(f'{name} no amber fill on rows', not r['amberFill'])
            if name == 'notready':
                titles = [t for t, _ in r['requirements']]
                check('notready lists every requirement', titles == ['Original report missing', 'Images incomplete'], titles)
                check('notready Original report missing opens Files', 'section=files' in r['requirements'][0][1], r['requirements'])
                check('notready Images incomplete opens Case details', 'section=overview' in r['requirements'][1][1], r['requirements'])
                check('notready step is the first requirement', 'Original report missing' in r['nextText'] and r['stepControl'] == ['Files'], r)
                check('notready card heading', r['cardHeading'] == 'Report not ready', r['cardHeading'])
            if name == 'review':
                check('review step is Assign Engineer alone', (r['labels'] == 0 and r['stepControl'] == ['Assign Engineer']) or (r['labels'] == 1 and 'Assign Engineer' in r['nextText'] and not r['stepControl']), r)
            if name == 'engineer':
                check('engineer step is a blocker in full', r['labels'] == 1 and r['nextItems'] == 1, r)
            # The chevron opens the card and the cookie names it; a reload keeps it open.
            page.click('[data-report-not-ready] [data-collapse-toggle]')
            r = page.evaluate(READ)
            check(f'{name} chevron opens', r['folded'] is False and r['bodyShown'] is True, r)
            check(f'{name} cookie names the opened card', FOLD in r['cookie'], r['cookie'])
            r = load(name)
            check(f'{name} reload keeps it open', r['folded'] is False and r['bodyShown'] is True, r)
            page.click('[data-report-not-ready] [data-collapse-toggle]')
            r = page.evaluate(READ)
            check(f'{name} chevron folds again', r['folded'] is True and FOLD not in r['cookie'], r)

        # A section fold keeps its default-open meaning: folding names it.
        context.clear_cookies()
        load('engineer')
        page.click('#section-overview [data-collapse-toggle]')
        cookie = page.evaluate('document.cookie')
        check('a section fold still names a folded section', 'case.overview' in cookie and FOLD not in cookie, cookie)
        check('no console error', not console, console)

        n = 0
        names = []
        for name in STATES:
            for width, height in WIDTHS:
                context.clear_cookies()
                load(name, width, height)
                n += 1
                path = SHOTS / f'{n:02d}-{name}-{width}.png'
                page.screenshot(path=str(path))
                names.append(path.name)
            if name != 'ready':
                context.clear_cookies()
                load(name)
                page.click('[data-report-not-ready] [data-collapse-toggle]')
                page.mouse.move(0, 0)
                page.wait_for_timeout(400)
                n += 1
                path = SHOTS / f'{n:02d}-{name}-open-1580.png'
                page.screenshot(path=str(path))
                names.append(path.name)
        browser.close()
    server.shutdown()

    result = {'date': date.today().isoformat(), 'fail': fail, 'okCount': len(ok), 'shots': names}
    (SHOTS / 'verification.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf8')
    print('RESULT', json.dumps({'fail': fail, 'okCount': len(ok)}))
    return 1 if fail else 0


if __name__ == '__main__':
    sys.exit(main())
