"""Self-check and screenshot capture for the v33 Valuation proposals.

    python check-valuation-designs.py            checks, then screenshots
    python check-valuation-designs.py --no-shots checks only

Loads every page in headless Chromium (Playwright) and asserts, for each of
the five designs: every state renders with no script error; the duplicate
figures are gone; every control today's section has is present; every word
on the page is one the application already uses; the figures move as the
live script moves them; nothing spills sideways at 1580, 1440 and 760. It
then writes the section screenshots and verification.json. This is evidence
about the mockup, not about the application.
"""
import json
import re
import sys
from datetime import date
from pathlib import Path

from playwright.sync_api import sync_playwright

CURRENT = Path(__file__).resolve().parent
REPO = CURRENT.parents[3]
SHOTS = CURRENT / 'v33-valuation-shots'
DESIGNS = ['a', 'b', 'c', 'd', 'e']
EDIT_STATES = ['fetched', 'recorded', 'own', 'refused', 'claimant-vat', 'pending', 'inspection', 'empty']
READ_STATES = ['fetched-read', 'recorded-read', 'empty-read']
LIVE_STATES = ['fetched', 'fetched-read', 'recorded', 'recorded-read', 'pending', 'empty-read']
WIDTHS = [(1580, 1000), (1440, 900), (760, 1000)]
REFUSAL = 'The valuation deductions exceed the value, so there is no figure to apply.'

ok = []
fail = []


def check(name, condition, detail=''):
    (ok if condition else fail).append(name if condition else f'{name} {detail}'.strip())


def url(design, state, options=''):
    query = f'?state={state}' + (f'&opt={options}' if options else '')
    return (CURRENT / f'pegasus_valuation_{design}_v33.html').as_uri() + query


# ---- the words the application already uses -----------------------------------

def application_strings():
    found = set()
    for relative in ['src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs', 'src/Pegasus.Web/Presentation/OperatorLabels.cs',
                     'src/Pegasus.Core/Assessment/ValuationCalculations.cs', 'src/Pegasus.Core/Assessment/Valuations.cs']:
        source = (REPO / relative).read_text(encoding='utf8')
        for match in re.finditer(r'"((?:[^"\\\n]|\\.)*)"', source):
            found.add(match.group(1).replace("\\'", "'").replace('\\"', '"'))
    return found


MONEY = r'\u00a3[\d,]+\.\d\d'
PATTERNS = [re.compile(pattern) for pattern in [
    rf'^(\+ |\u2212 )?{MONEY}$',
    rf'^Engineer\'s Value (\u2014|{MONEY})$',
    r'^from (Glass\'s|Brego|Super CAP|CAP|Cazana|AI market research) retail$',
    rf'^(Glass\'s|Brego|Super CAP|CAP|Cazana|AI market research) retail {MONEY}$',
    r'^(Glass\'s|Brego|Super CAP|CAP|Cazana) valuation is unavailable\. Contact an administrator or$',
    r'^\.$',
    r'^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) \d{4}$',
    r'^Oct 2026 \u00b7 42,000 miles \u00b7 6 Oct 2026$',
    r'^Researching \u00b7 October 2026$',
    r'^alex \u00b7 06 Oct 2026 \d\d:\d\d$',
    r'^\u2212(10|20) %$',
    r'^(Commercial VAT 20 %|Previous total loss \u2212(10|20) %)$',
    r'^(\+ VAT|\u2212 (10|20)% PTL|\+ [A-Za-z ]+|\u2212 condition)(, (\+ VAT|\u2212 (10|20)% PTL|\+ [A-Za-z ]+|\u2212 condition))*$',
    r'^\u2014$',
]]
# Synthetic data and words drawn by partials this folder does not read
# (the section head's Edit, the report summary, the AI source tag).
FIXTURE_WORDS = {"Glass's", 'Brego', 'Super CAP', 'CAP', 'Cazana', 'AI market research', 'AI', 'Tow bar', 'Roof bars',
                 'Edit', 'Guide source not disclosed', 'Collapse section', 'Other\u2026'}

VISIBLE_TEXT = """
() => {
  const section = document.getElementById('section-valuation');
  const walker = document.createTreeWalker(section, NodeFilter.SHOW_TEXT);
  const out = [];
  while (walker.nextNode()) {
    const node = walker.currentNode;
    const text = node.textContent.replace(/\\s+/g, ' ').trim();
    if (!text) continue;
    const element = node.parentElement;
    if (element.closest('option, script, style, template')) continue;
    if (element.closest('[hidden]')) continue;
    const style = getComputedStyle(element);
    if (style.display === 'none' || style.visibility === 'hidden') continue;
    let hiddenAncestor = false;
    for (let up = element; up && up !== section; up = up.parentElement) {
      if (getComputedStyle(up).display === 'none') { hiddenAncestor = true; break; }
    }
    if (!hiddenAncestor) out.push(text);
  }
  return out;
}
"""


def invented(page, allowed):
    words = page.evaluate(VISIBLE_TEXT)
    return [word for word in words
            if word not in allowed and word not in FIXTURE_WORDS and not any(p.match(word) for p in PATTERNS)]


def section_text(page):
    return ' '.join(page.evaluate(VISIBLE_TEXT))


def saved(page):
    page.wait_for_selector('[data-lease-line]:has-text("Saved")')


def engineer(page):
    return page.locator('[data-valuation-value="engineer"]').input_value()


def head(page):
    return page.locator('[data-valuation-head]').text_content().strip()


def record_shown(page):
    return page.locator('[data-out="eng-tag"] .src-tag').count() + page.locator('[data-valuation-history]').count() > 0


def spill(page):
    return page.evaluate("""() => {
      const section = document.getElementById('section-valuation');
      const bounds = section.getBoundingClientRect();
      let worst = document.documentElement.scrollWidth - window.innerWidth;
      section.querySelectorAll('*').forEach((node) => {
        const box = node.getBoundingClientRect();
        if (box.width && getComputedStyle(node).display !== 'none' && !node.closest('.sr-only')) {
          worst = Math.max(worst, box.right - bounds.right, bounds.left - box.left);
        }
      });
      return Math.round(worst);
    }""")


def run_checks(page, errors, allowed):
    for design in DESIGNS:
        tag = design.upper()

        # Every state renders, says nothing invented, and holds one figure.
        for state in EDIT_STATES + READ_STATES:
            page.goto(url(design, state))
            page.wait_for_selector('#section-valuation .panel-body > *')
            check(f'{tag} {state}: no script error', not errors and not page.evaluate('window.mockupErrors'), str(errors))
            errors.clear()
            words = invented(page, allowed)
            check(f'{tag} {state}: every word is the application\'s', not words, str(words))
            text = section_text(page)
            check(f'{tag} {state}: no Proposed Engineer\'s Value', "Proposed Engineer's Value" not in text)
            check(f'{tag} {state}: no Applied Engineer\'s Value', "Applied Engineer's Value" not in text)
            none_yet = text.count('None yet')
            check(f'{tag} {state}: None yet only with no source chosen', none_yet == (1 if state == 'empty' else 0), str(none_yet))
            check(f'{tag} {state}: one Engineer\'s Value cell', page.locator('[data-field="assessment.values.engineer"]').count() == 1)
            if state in READ_STATES:
                visible = page.locator('#section-valuation .panel-body').locator('input:visible, select:visible, button[data-act]:visible').count()
                check(f'{tag} {state}: reading shows no control', visible == 0, str(visible))

        # Coverage: every control today's section has.
        page.goto(url(design, 'fetched'))
        page.wait_for_selector('#section-valuation .panel-body > *')
        body = page.locator('#section-valuation .panel-body')
        counts = {
            'three value boxes': (body.locator('[data-valuation-value]').count(), 3),
            'five retail boxes': (body.locator('[data-valuation-retail]').count(), 5),
            'five trade boxes': (body.locator('[data-valuation-trade]').count(), 5),
            'five guide months': (body.locator('[data-valuation-entry-month]').count(), 5),
            'five Use this value': (body.locator('[data-valuation-use]').count(), 5),
            'one Get valuation on the connected source': (body.locator('[data-valuation-get]').count(), 1),
            'four standing notices': (body.locator('[data-valuation-not-connected]').count(), 4),
            'four report a problem': (body.locator('[data-act="problem"]').count(), 4),
            'valuation month': (body.locator('[data-valuation-month]').count(), 1),
            'AI market research': (body.locator('[data-valuation-source="ai-market-research"]').count(), 1),
            'previous total loss': (body.locator('select[data-bind="sel.ptl"]').count(), 1),
            'condition deduction': (body.locator('[data-bind="sel.ded"]').count(), 1),
            'commercial VAT': (body.locator('[data-bind="sel.vat"]').count(), 1),
            'two custom increases': (body.locator('[data-valuation-add]').count(), 2),
            'three report switches': (body.locator('[data-report-switch]').count(), 3),
        }
        for name, (got, want) in counts.items():
            check(f'{tag} coverage: {name}', got == want, f'{got} != {want}')
        check(f'{tag} fetched: head follows the box', head(page) == "Engineer's Value \u00a39,064.00", head(page))
        check(f'{tag} fetched: no record claimed for a figure never calculated', not record_shown(page))
        check(f'{tag} fetched: calculation names its source', "from Glass's retail" in section_text(page))

        # The figures move as the live script moves them.
        page.locator('#f-valuation-vat').check()
        check(f'{tag} VAT: the box takes the calculation', engineer(page) == '10877.00', engineer(page))
        check(f'{tag} VAT: the head follows at once', head(page) == "Engineer's Value \u00a310,877.00", head(page))
        check(f'{tag} VAT: the amount shows', '+ \u00a31,813.00' in section_text(page))
        saved(page)
        check(f'{tag} VAT: the record shows when the save lands', record_shown(page))
        page.locator('[data-valuation-value="engineer"]').fill('8000')
        saved(page)
        check(f'{tag} own figure: no record beside it', not record_shown(page))
        check(f'{tag} own figure: the head follows', head(page) == "Engineer's Value \u00a38,000.00", head(page))
        use = page.locator('[data-valuation-entry="glasses"] [data-valuation-use]')
        use.click()
        check(f'{tag} Use this value: the button says so', page.locator('[data-valuation-entry="glasses"] [data-valuation-use]').text_content().strip() == 'Using this value')
        check(f'{tag} Use this value: the box takes the calculation', engineer(page) == '10877.00', engineer(page))
        saved(page)
        check(f'{tag} Use this value: recorded', record_shown(page))
        page.locator('[data-bind="sel.ded"]').fill('99999')
        check(f'{tag} refusal: Core\'s own reason', page.locator('[data-valuation-error]').text_content().strip() == REFUSAL)
        check(f'{tag} refusal: the box goes back to the saved figure', engineer(page) == '10877.00', engineer(page))
        check(f'{tag} refusal: Use this value withdrawn', page.locator('[data-valuation-entry="glasses"] [data-valuation-use]').get_attribute('aria-pressed') == 'false')
        page.locator('[data-bind="sel.ded"]').fill('')
        check(f'{tag} refusal cleared', page.locator('[data-valuation-error]').count() == 0)
        page.locator('[data-valuation-entry="brego"] [data-valuation-use]').click()
        check(f'{tag} empty card: Use this value asks for a retail',
              'Enter the retail value on this card to use it.' in page.locator('[data-valuation-entry="brego"]').text_content())
        page.locator('[data-valuation-entry="brego"] [data-valuation-retail]').fill('8000')
        page.locator('[data-valuation-entry="brego"] [data-valuation-use]').click()
        check(f'{tag} typed card: becomes the basis', 'from Brego retail' in section_text(page))
        check(f'{tag} typed card: fills Retail value', page.locator('[data-valuation-value="retail"]').input_value() == '8000')
        check(f'{tag} typed card: the box takes its calculation', engineer(page) == '9600.00', engineer(page))
        saved(page)
        page.locator('[data-case-done]').click()
        page.wait_for_selector('[data-case-edit]')
        body = page.locator('#section-valuation .panel-body')
        check(f'{tag} Done: reading shows no control', body.locator('input:visible, select:visible, button[data-act]:visible').count() == 0)
        check(f'{tag} Done: the figure is kept', '\u00a39,600.00' in section_text(page))
        check(f'{tag} Done: the record is kept', record_shown(page))
        page.locator('[data-section-edit="valuation"]').click()
        page.wait_for_selector('[data-case-done]')
        check(f'{tag} Edit: the calculator opens on the recorded selection', page.locator('#f-valuation-vat').is_checked())
        check(f'{tag} behaviour: no script error', not errors and not page.evaluate('window.mockupErrors'), str(errors))
        errors.clear()

        # State particulars.
        page.goto(url(design, 'refused'))
        page.wait_for_selector('[data-valuation-error]')
        check(f'{tag} refused: the saved figure stands', engineer(page) == '9064.00', engineer(page))
        page.goto(url(design, 'claimant-vat'))
        page.wait_for_selector('#f-valuation-vat')
        check(f'{tag} claimant VAT: the tick is off and cannot be set', page.locator('#f-valuation-vat').is_disabled())
        check(f'{tag} claimant VAT: says why', 'Claimant is VAT registered' in section_text(page))
        page.goto(url(design, 'pending'))
        page.wait_for_selector('#section-valuation .panel-body > *')
        check(f'{tag} pending: Researching for its month', 'Researching \u00b7 October 2026' in section_text(page))
        page.goto(url(design, 'inspection'))
        page.wait_for_selector('#section-valuation .panel-body > *')
        check(f'{tag} Inspection view: no research control',
              page.locator('#section-valuation [data-valuation-month]').count() == 0 and page.locator('#section-valuation [data-valuation-source="ai-market-research"]').count() == 0)
        page.goto(url(design, 'own'))
        page.wait_for_selector('#section-valuation .panel-body > *')
        check(f'{tag} own: the typed figure stands with no record', engineer(page) == '8750.00' and not record_shown(page))

        # Both undecided choices, each way.
        for options, expect in [('record:tag', '[data-out="eng-tag"] .src-tag'), ('record:line', '[data-valuation-history]')]:
            for state in ['recorded', 'recorded-read']:
                page.goto(url(design, state, options))
                page.wait_for_selector('#section-valuation .panel-body > *')
                check(f'{tag} {state} {options}', page.locator(expect).count() == 1)
                check(f'{tag} {state} {options}: every word is the application\'s', not invented(page, allowed), str(invented(page, allowed)))
        for options, tools in [('ai:tools', 1), ('ai:own', 0)]:
            page.goto(url(design, 'recorded', options))
            page.wait_for_selector('#section-valuation .panel-body > *')
            check(f'{tag} recorded {options}', page.locator('#section-valuation [data-valuation-tools]').count() == tools)
            check(f'{tag} recorded {options}: research still offered', page.locator('#section-valuation [data-act="research"]').count() >= 1)
            check(f'{tag} recorded {options}: the AI figure is on the page', '\u00a312,900.00' in section_text(page))
            page.goto(url(design, 'pending', options))
            page.wait_for_selector('#section-valuation .panel-body > *')
            check(f'{tag} pending {options}', 'Researching \u00b7 October 2026' in section_text(page))
        check(f'{tag} options: no script error', not errors and not page.evaluate('window.mockupErrors'), str(errors))
        errors.clear()

    # Today's page is the capture: it still shows what the round is about.
    for state in LIVE_STATES:
        page.goto(url('live', state))
        page.wait_for_selector('#section-valuation .panel-body > *')
        check(f'Today {state}: no script error', not errors and not page.evaluate('window.mockupErrors'), str(errors))
        errors.clear()
    page.goto(url('live', 'fetched'))
    page.wait_for_selector('#section-valuation .panel-body > *')
    text = section_text(page)
    check('Today fetched: Proposed beside the box', "Proposed Engineer's Value" in text and '\u00a39,064.00' in text)
    check('Today fetched: Applied reads None yet', "Applied Engineer's Value" in text and 'None yet' in text)
    check('Today fetched: head reads a dash', head(page) == "Engineer's Value \u2014", head(page))


def run_widths(browser, heights):
    for width, height in WIDTHS:
        page = browser.new_page(viewport={'width': width, 'height': height})
        for design in DESIGNS + ['live']:
            for state in ['fetched', 'recorded', 'recorded-read']:
                page.goto(url(design, state))
                page.wait_for_selector('#section-valuation .panel-body > *')
                worst = spill(page)
                if design != 'live':
                    check(f'{design.upper()} {state} at {width}: nothing spills sideways', worst <= 1, str(worst))
                box = page.locator('#section-valuation').bounding_box()
                heights.setdefault(design, {}).setdefault(state, {})[str(width)] = round(box['height'])
        page.close()


def run_shots(browser):
    SHOTS.mkdir(exist_ok=True)
    for old in SHOTS.glob('*.png'):
        old.unlink()
    groups = [('live', 'fetched')] + [(design, 'fetched') for design in DESIGNS]
    groups += [(design, state) for state in ['recorded', 'recorded-read'] for design in ['live'] + DESIGNS]
    extras = [(design, state) for state in ['own', 'refused', 'pending', 'empty'] for design in DESIGNS]
    written = []
    for index, (design, state) in enumerate(groups + extras):
        widths = WIDTHS if (design, state) in groups else WIDTHS[:1]
        for width, _ in widths:
            # Tall enough for the whole section: the shot is the section, not the viewport.
            page = browser.new_page(viewport={'width': width, 'height': 3200})
            page.goto(url(design, state))
            page.wait_for_selector('#section-valuation .panel-body > *')
            page.evaluate("document.querySelector('.v33-mock').style.display = 'none'")
            name = f'{index:02d}-{design}-{state}-{width}.png'
            page.locator('#section-valuation').screenshot(path=str(SHOTS / name))
            written.append(name)
            page.close()
    return written


def main():
    allowed = application_strings()
    heights = {}
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch()
        page = browser.new_page(viewport={'width': 1580, 'height': 1000})
        errors = []
        page.on('console', lambda message: errors.append(message.text) if message.type == 'error' else None)
        page.on('pageerror', lambda error: errors.append(str(error)))
        run_checks(page, errors, allowed)
        page.close()
        run_widths(browser, heights)
        shots = [] if '--no-shots' in sys.argv else run_shots(browser)
        browser.close()
    result = {'fail': fail, 'okCount': len(ok)}
    print('RESULT ' + json.dumps(result))
    if '--no-shots' not in sys.argv:
        SHOTS.mkdir(exist_ok=True)
        (SHOTS / 'verification.json').write_text(json.dumps({
            'date': date.today().isoformat(),
            'result': result,
            'sectionHeights': heights,
            'screenshots': len(shots),
        }, indent=2) + '\n', encoding='utf8', newline='\n')
    return 1 if fail else 0


if __name__ == '__main__':
    sys.exit(main())
