"""Capture the running Management Reports page and diff its structure against
the mockup's Today file (not application evidence).

Runs against the local visual host (artifacts/ui-baseline-review/visual-host,
https://localhost:5240), whose fixtures hold no report data, so the live page
shows its empty states. Saves the post-script DOM to captured/, screenshots to
live-shots/, and prints the structural differences: panel headings, column
heads, labelled controls and button words, in page order.
"""
import json
import os
import sys
from pathlib import Path
from playwright.sync_api import sync_playwright

current = Path(__file__).resolve().parent
base = sys.argv[1] if len(sys.argv) > 1 else 'https://localhost:5240'
(current / 'captured').mkdir(exist_ok=True)
(current / 'live-shots').mkdir(exist_ok=True)

STRUCTURE = """() => {
  const main = document.querySelector('main');
  const words = el => el.textContent.replace(/\\s+/g, ' ').trim();
  return {
    headings: [...main.querySelectorAll('h1, h2, h3')].map(words),
    columns: [...main.querySelectorAll('th')].map(th => words(th).replace(/[\\u2191\\u2193]/g, '').trim()),
    controls: [...main.querySelectorAll('label')].map(words).filter(Boolean),
    buttons: [...main.querySelectorAll('button, a.btn')].map(words).filter(Boolean),
    metrics: [...main.querySelectorAll('.metric-label')].map(words),
    meta: [...main.querySelectorAll('.panel-head .meta')].map(words),
    notes: [...main.querySelectorAll('.admin-report-note')].map(words),
    arrowsWhenSorted: null,
  };
}"""

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, executable_path=os.environ.get('PEGASUS_CHROME') or None)
    context = browser.new_context(ignore_https_errors=True, viewport={'width': 1440, 'height': 900})
    page = context.new_page()
    page.goto(base + '/Administration/Reports')
    page.wait_for_load_state('networkidle')
    (current / 'captured' / 'frame-reports-live.html').write_text(page.evaluate('document.documentElement.outerHTML'), encoding='utf-8')
    live = page.evaluate(STRUCTURE)
    for width, height in ((1580, 1000), (1440, 900), (760, 1000)):
        page.set_viewport_size({'width': width, 'height': height})
        page.screenshot(path=str(current / 'live-shots' / f'live-reports-{width}.png'))
    page.set_viewport_size({'width': 1580, 'height': 1000})
    page.screenshot(path=str(current / 'live-shots' / 'live-reports-full-1580.png'), full_page=True)
    page.goto(base + '/Administration/Reports?sort=reports&dir=asc')
    page.wait_for_load_state('networkidle')
    live['arrowsWhenSorted'] = page.evaluate("() => { const th = document.querySelector('th[aria-sort]'); return th ? th.innerText.replace(/\\s+/g, ' ').trim() : null; }")

    mock_page = context.new_page()
    mock_page.goto((current / 'pegasus_management_reports_live_v37.html').as_uri() + '?state=empty&embed=1')
    mock_page.wait_for_function('window.mockupReady === true')
    mock = mock_page.evaluate(STRUCTURE)
    mock_page.goto((current / 'pegasus_management_reports_live_v37.html').as_uri() + '?sort=reports&dir=asc&embed=1')
    mock_page.wait_for_function('window.mockupReady === true')
    mock['arrowsWhenSorted'] = mock_page.evaluate("() => { const th = document.querySelector('th[aria-sort]'); return th ? th.innerText.replace(/\\s+/g, ' ').trim() : null; }")
    browser.close()

diff = {}
for key in live:
    if live[key] != mock[key]:
        diff[key] = {'live': live[key], 'mockup': mock[key]}
(current / 'captured' / 'structure-reports-live.json').write_text(json.dumps({'live': live, 'mockupToday': mock, 'differences': diff}, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps(diff, indent=2, ensure_ascii=False) if diff else 'No structural difference between the live page and the mockup\'s Today file.')
