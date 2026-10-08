"""Stage 2 conformance check for the implemented Valuation section (v34 cards).

    python check-valuation-implementation.py <folder of captured pages>

The folder holds the Case pages the application rendered after Stage 2, made
by a temporary test (never committed) that wrote GetHtmlAsync output for each
state to the folder named by PEGASUS_V34_CAPTURE. The script restyles those
pages with the live CSS from this checkout, then:

  1. screenshots #section-valuation in each state at 1580, 1440 and 760 into
     v34-conformance/, for side-by-side comparison with v34-shots/;
  2. runs the real site.js and case-workspace.js against the edit pages in
     headless Chromium with fetch stubbed (the preview answers Core's
     arithmetic re-worked here; saves are left waiting), and checks the
     section's script: a click anywhere on a card choosing it and marking
     the decision, a card with no retail saying why, the preview filling the
     box and the amounts, the previous total loss switch, and the head and
     the recorded source's word following a save.

The pages are the application's own markup and scripts, but they are opened
from disk with stubbed requests: this is not a signed-in walk of a running
Pegasus, and it does not exercise a real save. Set PEGASUS_CHROME to a
Chromium executable when Playwright's own browser does not match its driver.
"""
import base64
import json
import os
import re
import sys
import tempfile
from datetime import date
from pathlib import Path

from playwright.sync_api import sync_playwright

CURRENT = Path(__file__).resolve().parent
ROOT = CURRENT.parents[3]
WEB = ROOT / 'src' / 'Pegasus.Web' / 'wwwroot'
RAW = Path(sys.argv[1]).resolve()
OUT = Path(tempfile.mkdtemp(prefix='v34-conformance-'))
SHOTS = CURRENT / 'v34-conformance'
SHOTS.mkdir(exist_ok=True)


def css():
    site = (WEB / 'css' / 'site.css').read_text(encoding='utf8')
    regular = base64.b64encode((WEB / 'fonts' / 'inter' / 'InterVariable.woff2').read_bytes()).decode()
    italic = base64.b64encode((WEB / 'fonts' / 'inter' / 'InterVariable-Italic.woff2').read_bytes()).decode()
    site = re.sub(r'url\(\.\./fonts/inter/InterVariable\.[0-9a-z]+\.woff2\)', lambda m: f'url(data:font/woff2;base64,{regular})', site)
    site = site.replace('url(../fonts/inter/InterVariable-Italic.woff2)', f'url(data:font/woff2;base64,{italic})')
    return site + '\n' + (WEB / 'css' / 'case-workspace.css').read_text(encoding='utf8')


STUB = r"""
window.__previews = 0;
window.fetch = function (url, options) {
  url = String(url);
  function answer(body, type) { return Promise.resolve(new Response(body, { status: 200, headers: { 'Content-Type': type || 'text/html' } })); }
  if (url.indexOf('handler=PreviewValuation') >= 0) {
    window.__previews += 1;
    var form = options.body;
    var money = function (n) { return '£' + n.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); };
    var whole = function (n) { return n < 0 ? -Math.round(-n) : Math.round(n); };
    if (!form.get('selection.GuideValuationId') && !form.get('selection.GuideSource')) {
      return answer('<div class="muted" data-valuation-none>None yet</div>');
    }
    var retail = parseFloat(form.get('basisRetail') || '0') || 0;
    if (!(retail > 0)) { return answer('<div class="notice notice--danger" role="alert" data-valuation-error>A guide retail value is required before a valuation can be calculated.</div>'); }
    var vatOn = form.get('selection.CommercialVat') === 'true';
    var vat = vatOn ? whole(retail * 0.2) : 0;
    var pct = parseFloat(form.get('selection.PriorTotalLossPercentage') || '0') || 0;
    var ptl = pct ? whole((retail + vat) * pct / 100) : 0;
    var amounts = form.getAll('selection.AdditionAmount');
    var adds = form.getAll('selection.AdditionSelected').reduce(function (sum, index) { return sum + (parseFloat(amounts[Number(index)] || '0') || 0); }, 0);
    var ded = parseFloat(form.get('selection.ConditionDeduction') || '0') || 0;
    var proposal = whole(retail + vat - ptl + adds - ded);
    if (proposal < 0) { return answer('<div class="notice notice--danger" role="alert" data-valuation-error>The valuation deductions exceed the value, so there is no figure to apply.</div>'); }
    return answer('<span hidden data-valuation-proposal="' + proposal.toFixed(2) + '" data-valuation-vat-amount="' + (vatOn ? '+ ' + money(vat) : '') + '" data-valuation-ptl-amount="' + (pct ? '− ' + money(ptl) : '') + '"></span>');
  }
  // Every other request (the save, the lease heartbeat, Get valuation) is left
  // waiting: the harness is about the section's own script.
  return new Promise(function () {});
};
"""


def page_html(name, scripts):
    html = (RAW / name).read_text(encoding='utf8')
    html = re.sub(r'<link rel="stylesheet"[^>]*>', '', html)
    html = html.replace('</head>', '<style>' + css() + '</style></head>', 1)
    mark = base64.b64encode((WEB / 'images' / 'pegasus-mark-refined-128.png').read_bytes()).decode()
    html = re.sub(r'src="/images/pegasus-mark-refined-128\.[0-9a-z]+\.png"', lambda m: f'src="data:image/png;base64,{mark}"', html)
    html = re.sub(r'<section class="(record-section[^"]*)" id="section-(?!valuation")', r'<section class="\1 is-collapsed" id="section-', html)

    def inline(match):
        if not scripts:
            return ''
        file = re.sub(r'\.[0-9a-z]+\.js$', '.js', match.group(1))
        source = (WEB / 'js' / file).read_text(encoding='utf-8-sig')
        return '<script>' + source.replace('</script', '<\\/script') + '</script>'

    html = re.sub(r'<script src="/js/([^"]+)"></script>', inline, html)
    if scripts:
        html = html.replace('<head>', '<head><script>' + STUB + '</script>', 1)
    target = OUT / (('live-' if scripts else 'still-') + name)
    target.write_text(html, encoding='utf8')
    return target


ok, fail = [], []


def check(name, condition, detail=''):
    (ok if condition else fail).append(name if condition else f'{name} {detail}'.strip())


def text(page, selector):
    return (page.locator(selector).first.text_content() or '').strip()


def box_value(page):
    return page.locator('[data-valuation-value="engineer"]').input_value()


def wait_box(page, value):
    page.wait_for_function(f"document.querySelector('[data-valuation-value=\"engineer\"]').value === '{value}'", timeout=4000)


def chosen(page):
    return page.evaluate("[...document.querySelectorAll('#section-valuation .valuation-card.sel')].map(c => c.getAttribute('data-valuation-entry') || c.getAttribute('data-valuation-source-card'))")


def words(page):
    return page.evaluate("[...document.querySelectorAll('[data-valuation-chosen-word]')].filter(w => !w.hidden).map(w => w.closest('.valuation-card').getAttribute('data-valuation-entry') || 'research')")


def used(page):
    return page.evaluate("!document.querySelector('[data-valuation-use-input]').disabled")


chrome = os.environ.get('PEGASUS_CHROME')
with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path=chrome) if chrome else playwright.chromium.launch()

    # 1. Still screenshots of the rendered section, by the mockup shot each matches.
    states = [('edit-fetched', '01-fetched'), ('read-fetched', '02-fetched-read'), ('edit-applied', '03-recorded'),
              ('read-applied', '04-recorded-read'), ('edit-pending', '08-pending'), ('edit-empty', '10-empty'),
              ('read-empty', '11-empty-read')]
    heights = {}
    for state, mock in states:
        target = page_html(state + '.html', scripts=False)
        for width, height in ((1580, 1000), (1440, 900), (760, 1000)):
            page = browser.new_page(viewport={'width': width, 'height': height})
            page.goto(target.as_uri())
            section = page.locator('#section-valuation')
            box = section.bounding_box()
            page.screenshot(path=str(SHOTS / f'{state}-{width}.png'), full_page=True, clip=box)
            heights.setdefault(state, {})[str(width)] = round(box['height'])
            spill = page.evaluate("""() => { const s = document.getElementById('section-valuation'); const b = s.getBoundingClientRect(); let w = document.documentElement.scrollWidth - innerWidth; s.querySelectorAll('*').forEach(n => { const r = n.getBoundingClientRect(); if (r.width && getComputedStyle(n).display !== 'none' && !n.closest('.sr-only') && getComputedStyle(n).position !== 'absolute') w = Math.max(w, r.right - b.right, b.left - r.left); }); return Math.round(w); }""")
            check(f'{state} at {width}: nothing spills sideways', spill <= 1, str(spill))
            if width == 1580:
                check(f'{state}: no Use this value, no Retail or Trade box',
                      page.locator('[data-valuation-use]').count() == 0
                      and page.locator('[data-valuation-value="retail"], [data-valuation-value="trade"]').count() == 0)
            page.close()

    # 2. The real scripts, in a browser.
    errors = []
    page = browser.new_page(viewport={'width': 1580, 'height': 2400})
    page.on('pageerror', lambda error: None if 'sendBeacon' in str(error) else errors.append(str(error)))
    page.goto(page_html('edit-fetched.html', scripts=True).as_uri())
    page.wait_for_selector('#section-valuation .valuation-card')
    check('fetched: CAP is chosen and says Selected', chosen(page) == ['cap'] and words(page) == ['cap'], f'{chosen(page)} {words(page)}')
    check('fetched: head reads the saved figure', text(page, '[data-valuation-head]') == "Engineer's Value £3,100.00", text(page, '[data-valuation-head]'))
    check('fetched: the decision is not marked until a click', not used(page))
    check('fetched: no recorded word', page.locator('[data-valuation-recorded-word]').is_hidden())

    page.locator('#f-valuation-vat').check()
    wait_box(page, '3720.00')
    check('VAT: the preview fills the box', box_value(page) == '3720.00')
    check('VAT: its amount stands on its row', text(page, '[data-valuation-amount="vat"]') == '+ £620.00', text(page, '[data-valuation-amount="vat"]'))
    check('VAT: its row is drawn on', 'on' in (page.locator('[data-valuation-vat-wrap]').get_attribute('class') or ''))

    check('PTL: the percentages wait for the tick box', page.locator('[data-valuation-ptl]').first.is_disabled())
    page.locator('#f-valuation-ptl').check()
    wait_box(page, '3348.00')
    check('PTL: ticking starts at -10 %', page.locator('[data-valuation-ptl][value="10"]').is_checked())
    check('PTL: its amount stands beside it', text(page, '[data-valuation-amount="ptl"]') == '− £372.00', text(page, '[data-valuation-amount="ptl"]'))
    page.locator('.valuation-ptl-switch label:has(input[value="20"])').click()
    wait_box(page, '2976.00')
    check('PTL: -20 %', page.locator('[data-valuation-ptl][value="20"]').is_checked())
    page.locator('#f-valuation-ptl').uncheck()
    wait_box(page, '3720.00')
    check('PTL: unticking clears the percentage', page.locator('[data-valuation-ptl]:checked').count() == 0 and text(page, '[data-valuation-amount="ptl"]') == '')

    page.locator('#f-valuation-deduction').fill('99999')
    page.wait_for_selector('[data-valuation-preview-host] [data-valuation-error]')
    check('refusal: the amounts clear', text(page, '[data-valuation-amount="vat"]') == '' and text(page, '[data-valuation-amount="ptl"]') == '')
    check('refusal: the box goes back to its saved figure', box_value(page) == '3100.00', box_value(page))
    page.locator('#f-valuation-deduction').fill('')
    wait_box(page, '3720.00')

    # The whole card is the target.
    page.locator('label[for="f-valuation-brego-retail"]').click()
    wait_box(page, '3660.00')
    check('card: a click on a label chooses it', chosen(page) == ['brego'] and words(page) == ['brego'], f'{chosen(page)} {words(page)}')
    check('card: the click marks the decision', used(page))
    check('card: the basis is named', text(page, '[data-valuation-basis-name]') == 'from Brego retail', text(page, '[data-valuation-basis-name]'))
    page.evaluate("window.__box = document.getElementById('f-valuation-glasses-trade')")
    page.locator('#f-valuation-glasses-trade').click()
    wait_box(page, '3540.00')
    check('card: a click in a box chooses it', chosen(page) == ['glasses'])
    check('card: that box keeps its focus and is not redrawn', page.evaluate("document.activeElement === window.__box && window.__box.isConnected"))
    bounds = page.locator('[data-valuation-entry="cazana"]').bounding_box()
    page.mouse.click(bounds['x'] + bounds['width'] - 6, bounds['y'] + bounds['height'] - 6)
    wait_box(page, '3450.00')
    check('card: a click on its padding chooses it', chosen(page) == ['cazana'])
    page.locator('[data-valuation-entry="super-cap"] h3').click()
    check('card: no retail says why', page.locator('[data-valuation-entry="super-cap"] [data-valuation-needs-retail]').is_visible())
    check('card: and is not chosen', chosen(page) == ['cazana'])
    page.locator('#f-valuation-super-cap-retail').click()
    check('card: a click into its box only focuses it', chosen(page) == ['cazana'] and page.evaluate("document.activeElement.id") == 'f-valuation-super-cap-retail')
    page.keyboard.type('4000')
    check('card: typing a retail answers the sentence', page.locator('[data-valuation-entry="super-cap"] [data-valuation-needs-retail]').is_hidden())
    page.locator('[data-valuation-entry="super-cap"] h3').click()
    wait_box(page, '4800.00')
    check('card typed in this edit: chosen by its source',
          chosen(page) == ['super-cap'] and page.evaluate("document.querySelector('[data-valuation-source-input]').value") == 'SuperCap'
          and page.locator('[data-valuation-basis]:checked').count() == 0)
    page.locator('[data-valuation-entry="cap"]').focus()
    page.keyboard.press('Enter')
    wait_box(page, '3720.00')
    check('card: Enter on a card chooses it', chosen(page) == ['cap'] and page.locator('[data-valuation-source-input]').is_disabled())

    # A landed save carries the saved state: the head and the word follow it.
    page.evaluate("""() => { const saved = document.querySelector('[data-valuation-recorded-state]'); saved.value = JSON.stringify({ head: "Engineer's Value £3,720.00", value: '3720.00', word: 'CAP', research: false }); saved.dispatchEvent(new CustomEvent('pegasus:carried-forward', { bubbles: true })); }""")
    check('save landed: the head follows', text(page, '[data-valuation-head]') == "Engineer's Value £3,720.00", text(page, '[data-valuation-head]'))
    check('save landed: the recorded word shows', page.locator('[data-valuation-recorded-word]').is_visible() and text(page, '[data-valuation-recorded-word]') == 'CAP')
    page.locator('[data-valuation-value="engineer"]').fill('3600')
    check('typed over: the word goes at once', page.locator('[data-valuation-recorded-word]').is_hidden())
    check('typed over: the decision to use the calculation is withdrawn', not used(page))
    check('fetched page: no script error', not errors, str(errors))
    errors.clear()
    page.close()

    # The recorded calculation, opened for editing.
    page = browser.new_page(viewport={'width': 1580, 'height': 2400})
    page.on('pageerror', lambda error: None if 'sendBeacon' in str(error) else errors.append(str(error)))
    page.goto(page_html('edit-applied.html', scripts=True).as_uri())
    page.wait_for_selector('#section-valuation .valuation-card')
    check('recorded: CAP is chosen', chosen(page) == ['cap'])
    check('recorded: the word shows on opening', page.locator('[data-valuation-recorded-word]').is_visible() and text(page, '[data-valuation-recorded-word]') == 'CAP')
    check('recorded: the amounts stand beside their controls', text(page, '[data-valuation-amount="vat"]') == '+ £620.00' and text(page, '[data-valuation-amount="ptl"]') == '− £372.00',
          f"{text(page, '[data-valuation-amount=\"vat\"]')} {text(page, '[data-valuation-amount=\"ptl\"]')}")
    check('recorded: the tick box and -10 % open on', page.locator('#f-valuation-ptl').is_checked() and page.locator('[data-valuation-ptl][value="10"]').is_checked())
    check('recorded: nothing is calculated until a change', page.evaluate('window.__previews') == 0)
    page.locator('[data-valuation-card].valuation-card--research h3').click()
    check('research chosen: it says Selected', words(page) == ['research'], str(words(page)))
    check('recorded page: no script error', not errors, str(errors))
    page.close()
    browser.close()

result = {'fail': fail, 'okCount': len(ok)}
print('RESULT ' + json.dumps(result))
(SHOTS / 'verification.json').write_text(json.dumps({
    'date': date.today().isoformat(),
    'result': result,
    'sectionHeights': heights,
}, indent=2) + '\n', encoding='utf8', newline='\n')
sys.exit(1 if fail else 0)
