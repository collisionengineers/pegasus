"""Stage 2 conformance check for the implemented Valuation section (design D).

    python check-valuation-implementation.py <folder of captured pages>

The folder holds the Case pages the application rendered after Stage 2, made
by the temporary test in captured/capture-implementation-test.patch (apply
it, set PEGASUS_V33_CAPTURE to the folder, run the one test, revert it). The
script restyles those pages with the live CSS from this checkout, then:

  1. screenshots #section-valuation in each state at 1580, 1440 and 760 into
     v33-conformance/, for side-by-side comparison with v33-valuation-shots/;
  2. runs the real site.js and case-workspace.js against the edit pages in
     headless Chromium with fetch stubbed (the preview answers Core's
     arithmetic re-worked here; saves are left waiting), and checks the
     section's script: the preview filling the box and the amounts, the
     calculation moving under the chosen row, and the head and the recorded
     source's word following a save.

The pages are the application's own markup and scripts, but they are opened
from disk with stubbed requests: this is not a signed-in walk of a running
Pegasus, and it does not exercise a real save.
"""
import base64
import json
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
OUT = Path(tempfile.mkdtemp(prefix='v33-conformance-'))
SHOTS = CURRENT / 'v33-conformance'
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
    var money = function (n) { return '\u00a3' + n.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); };
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
    return answer('<span hidden data-valuation-proposal="' + proposal.toFixed(2) + '" data-valuation-vat-amount="' + (vatOn ? '+ ' + money(vat) : '') + '" data-valuation-ptl-amount="' + (pct ? '\u2212 ' + money(ptl) : '') + '"></span>');
  }
  // Every other request (the save, the lease heartbeat) is left waiting: the
  // harness is about the section's own script.
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


with sync_playwright() as playwright:
    browser = playwright.chromium.launch()

    # 1. Still screenshots of the rendered section.
    states = [('edit-fetched', '04-d-fetched'), ('edit-applied', '10-d-recorded'), ('read-applied', '16-d-recorded-read'),
              ('read-own', None), ('edit-pending', '31-d-pending'), ('read-empty', None), ('read-fetched', None)]
    heights = {}
    for state, mock in states:
        target = page_html(state + '.html', scripts=False)
        for width in (1580, 1440, 760):
            page = browser.new_page(viewport={'width': width, 'height': 3200})
            page.goto(target.as_uri())
            section = page.locator('#section-valuation')
            section.screenshot(path=str(SHOTS / f'{state}-{width}.png'))
            box = section.bounding_box()
            heights.setdefault(state, {})[str(width)] = round(box['height'])
            spill = page.evaluate("""() => { const s = document.getElementById('section-valuation'); const b = s.getBoundingClientRect(); let w = document.documentElement.scrollWidth - innerWidth; s.querySelectorAll('*').forEach(n => { const r = n.getBoundingClientRect(); if (r.width && getComputedStyle(n).display !== 'none' && !n.closest('.sr-only') && getComputedStyle(n).position !== 'absolute') w = Math.max(w, r.right - b.right, b.left - r.left); }); return Math.round(w); }""")
            check(f'{state} at {width}: nothing spills sideways', spill <= 1, str(spill))
            page.close()

    # 2. The real scripts, in a browser.
    errors = []
    page = browser.new_page(viewport={'width': 1580, 'height': 2400})
    page.on('pageerror', lambda error: None if 'sendBeacon' in str(error) else errors.append(str(error)))
    page.goto(page_html('edit-fetched.html', scripts=True).as_uri())
    page.wait_for_selector('#section-valuation [data-valuation-open]')
    check('fetched: the calculation stands under the chosen row',
          page.evaluate("document.querySelector('[data-valuation-open]').previousElementSibling.getAttribute('data-valuation-entry')") == 'glasses')
    check('fetched: head reads the saved figure', text(page, '[data-valuation-head]') == "Engineer's Value \u00a39,064.00", text(page, '[data-valuation-head]'))
    check('fetched: no recorded word', page.locator('[data-valuation-recorded-word]').is_hidden())
    page.locator('input[name="selection.CommercialVat"]').check()
    page.wait_for_function("document.querySelector('[data-valuation-value=\"engineer\"]').value === '10877.00'")
    check('VAT: the preview fills the box', page.locator('[data-valuation-value="engineer"]').input_value() == '10877.00')
    check('VAT: its amount stands in its cell', text(page, '[data-valuation-amount="vat"]') == '+ \u00a31,813.00', text(page, '[data-valuation-amount="vat"]'))
    page.locator('#f-valuation-ptl').select_option('10')
    page.wait_for_function("document.querySelector('[data-valuation-value=\"engineer\"]').value === '9789.00'")
    check('PTL: its amount stands in its cell', text(page, '[data-valuation-amount="ptl"]') == '\u2212 \u00a31,088.00', text(page, '[data-valuation-amount="ptl"]'))
    page.locator('#f-valuation-deduction').fill('99999')
    page.wait_for_selector('[data-valuation-preview-host] [data-valuation-error]')
    check('refusal: the amounts clear', text(page, '[data-valuation-amount="vat"]') == '' and text(page, '[data-valuation-amount="ptl"]') == '')
    check('refusal: the box goes back to its saved figure', page.locator('[data-valuation-value="engineer"]').input_value() == '9064.00', page.locator('[data-valuation-value="engineer"]').input_value())
    page.locator('#f-valuation-deduction').fill('')
    page.wait_for_function("document.querySelector('[data-valuation-value=\"engineer\"]').value === '9789.00'")
    # A source typed in this edit, then used: the block moves under its row.
    page.locator('[data-valuation-entry="brego"] [data-valuation-use]').click()
    check('empty source: Use this value asks for a retail', 'Enter the retail value on this card to use it.' in page.locator('[data-valuation-entry="brego"]').text_content())
    page.locator('#f-valuation-brego-retail').fill('8000')
    page.locator('[data-valuation-entry="brego"] [data-valuation-use]').click()
    check('Use this value: the calculation moves under the chosen row',
          page.evaluate("document.querySelector('[data-valuation-open]').previousElementSibling.getAttribute('data-valuation-entry')") == 'brego')
    check('Use this value: the row is drawn chosen', 'sel' in page.locator('[data-valuation-entry="brego"]').get_attribute('class'))
    check('Use this value: the head names the source', text(page, '[data-valuation-basis-name]') == 'from Brego retail', text(page, '[data-valuation-basis-name]'))
    check('Use this value: Retail value is filled', page.locator('[data-valuation-value="retail"]').input_value() == '8000')
    page.wait_for_function("document.querySelector('[data-valuation-value=\"engineer\"]').value === '8640.00'")
    check('Use this value: the box takes the calculation', True)
    check('Use this value: the calculator kept its controls through the move', page.locator('input[name="selection.CommercialVat"]').is_checked() and page.locator('#f-valuation-ptl').input_value() == '10')
    # A click on a row that has a retail chooses it.
    page.locator('[data-valuation-entry="glasses"] h3').click()
    check('row click: the calculation moves back', page.evaluate("document.querySelector('[data-valuation-open]').previousElementSibling.getAttribute('data-valuation-entry')") == 'glasses')
    # A landed save carries the saved state: the head and the word follow it.
    page.wait_for_function("document.querySelector('[data-valuation-value=\"engineer\"]').value === '9789.00'")
    page.evaluate("""() => { const saved = document.querySelector('[data-valuation-recorded-state]'); saved.value = JSON.stringify({ head: "Engineer's Value \u00a39,789.00", value: '9789.00', word: "Glass's", research: false }); saved.dispatchEvent(new CustomEvent('pegasus:carried-forward', { bubbles: true })); }""")
    check('save landed: the head follows', text(page, '[data-valuation-head]') == "Engineer's Value \u00a39,789.00", text(page, '[data-valuation-head]'))
    check('save landed: the recorded word shows', page.locator('[data-valuation-recorded-word]').is_visible() and text(page, '[data-valuation-recorded-word]') == "Glass's")
    page.locator('[data-valuation-value="engineer"]').fill('9000')
    check('typed over: the word goes at once', page.locator('[data-valuation-recorded-word]').is_hidden())
    check('typed over: the head waits for the save', text(page, '[data-valuation-head]') == "Engineer's Value \u00a39,789.00")
    page.locator('[data-valuation-value="engineer"]').fill('9789')
    check('typed back: the word returns', page.locator('[data-valuation-recorded-word]').is_visible())
    check('fetched page: no script error', not errors, str(errors))
    errors.clear()
    page.close()

    # The recorded calculation, opened for editing.
    page = browser.new_page(viewport={'width': 1580, 'height': 2400})
    page.on('pageerror', lambda error: None if 'sendBeacon' in str(error) else errors.append(str(error)))
    page.goto(page_html('edit-applied.html', scripts=True).as_uri())
    page.wait_for_selector('#section-valuation [data-valuation-open]')
    check('recorded: the word shows on opening', page.locator('[data-valuation-recorded-word]').is_visible() and text(page, '[data-valuation-recorded-word]') == "Glass's")
    check('recorded: the amounts stand in their cells', text(page, '[data-valuation-amount="vat"]') == '+ \u00a32,500.00' and text(page, '[data-valuation-amount="ptl"]') == '\u2212 \u00a31,500.00')
    check('recorded: nothing is calculated until a change', page.evaluate('window.__previews') == 0)
    research = page.locator('.valuation-source--research [data-valuation-use]')
    check('recorded: the research row offers Use this value', research.count() == 1)
    research.click()
    check('research chosen: the calculation moves under its row',
          page.evaluate("document.querySelector('[data-valuation-open]').previousElementSibling.classList.contains('valuation-source--research')"))
    check('research chosen: the word goes, the figure is no longer the recorded one', page.locator('[data-valuation-recorded-word]').is_hidden() or page.locator('[data-valuation-value="engineer"]').input_value() == '13425.00')
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
